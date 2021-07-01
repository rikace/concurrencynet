using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp.Parallelx
{

    class FastChannel : SafeDisposable
    {
        static int _lastMessageID, _lastObjectID;

        public readonly byte DomainAddress;

        readonly OutPipe _outPipe;
        readonly InPipe _inPipe;
        readonly Module _module; // The module for which serialization is optimized.
        readonly object _proxiesLock = new object();
        readonly Dictionary<int, IProxy> _proxiesByID = new Dictionary<int, IProxy>();
        readonly Dictionary<object, IProxy> _proxiesByObject = new Dictionary<object, IProxy>();

        readonly Dictionary<int, Action<MessageType, object>> _pendingReplies =
            new Dictionary<int, Action<MessageType, object>>();

        int _messagesReceived;

        public int MessagesReceived
        {
            get { return _messagesReceived; }
        }

        enum MessageType : byte
        {
            Activation,
            Deactivation,
            MethodCall,
            ReturnValue,
            ReturnException
        }

        public FastChannel(string name, bool isOwner, Module module)
        {
            _module = module; // Types belonging to this module will serialize faster

            DomainAddress = (byte) (isOwner ? 1 : 2);

            _outPipe = new OutPipe(name + (isOwner ? ".A" : ".B"), isOwner);
            _inPipe = new InPipe(name + (isOwner ? ".B" : ".A"), isOwner, OnMessageReceived);
        }

        protected override void DisposeCore()
        {
            base.DisposeCore();
            lock (_proxiesLock)
            {
                _proxiesByID.Clear();
                _proxiesByObject.Clear();
            }

            _inPipe.Dispose();
            _outPipe.Dispose();
        }

        IProxy FindProxy(int objectID)
        {
            IProxy proxy;
            lock (_proxiesLock) _proxiesByID.TryGetValue(objectID, out proxy);
            return proxy;
        }

        IProxy RegisterLocalProxy(IProxy proxy)
        {
            lock (_proxiesLock)
            {
                // Avoid multiple identities for the same object:
                IProxy existingProxy;
                bool alreadyThere = _proxiesByObject.TryGetValue(proxy.LocalInstanceUntyped, out existingProxy);
                if (alreadyThere) return existingProxy;

                int objectID = Interlocked.Increment(ref _lastObjectID);
                proxy.RegisterLocal(this, objectID, () => UnregisterLocalProxy(proxy, objectID));

                _proxiesByID[objectID] = proxy;
                _proxiesByObject[proxy] = proxy;
                return proxy;
            }
        }

        void UnregisterLocalProxy(IProxy proxy, int objectID)
        {
            lock (_proxiesLock)
            {
                _proxiesByID.Remove(objectID);
                _proxiesByObject.Remove(proxy.LocalInstanceUntyped);
            }
        }

        /// <summary>Instantiates an object remotely. To release it, you can either call Disconnect on the proxy returned
        /// or wait for its finalizer to do the same.s</summary>
        public Task<Proxy<TRemote>> Activate<TRemote>() where TRemote : class
        {
            lock (DisposeLock)
            {
                AssertSafe();
                int messageNumber = Interlocked.Increment(ref _lastMessageID);

                var ms = new MemoryStream();
                var writer = new BinaryWriter(ms);
                writer.Write((byte) MessageType.Activation);
                writer.Write(messageNumber);
                SerializeType(writer, typeof(TRemote));
                writer.Flush();

                var task = GetResponseFuture<Proxy<TRemote>>(messageNumber);
                _outPipe.Write(ms.ToArray());

                return task;
            }
        }

        internal void InternalDeactivate(int objectID)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write((byte) MessageType.Deactivation);
            writer.Write(objectID);
            writer.Flush();

            lock (DisposeLock)
            {
                if (IsDisposed) return;
                _outPipe.Write(ms.ToArray());
            }
        }

        internal Task<TResult> SendMethodCall<TResult>(Expression expressionBody, int objectID, bool awaitRemoteTask)
        {
            lock (DisposeLock)
            {
                AssertSafe();
                int messageNumber = Interlocked.Increment(ref _lastMessageID);
                byte[] payload = SerializeMethodCall(expressionBody, messageNumber, objectID, awaitRemoteTask);
                var task = GetResponseFuture<TResult>(messageNumber);
                _outPipe.Write(payload);
                return task;
            }
        }

        Task<T> GetResponseFuture<T>(int messageNumber)
        {
            var tcs = new TaskCompletionSource<T>();

            lock (_pendingReplies)
                _pendingReplies.Add(messageNumber, (msgType, value) =>
                {
                    if (msgType == MessageType.ReturnValue)
                        tcs.SetResult((T) value);
                    else
                    {
                        var ex = (Exception) value;
                        MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (preserveStackTrace != null) preserveStackTrace.Invoke(ex, null);
                        tcs.SetException(ex);
                    }
                });

            return tcs.Task;
        }

        void OnMessageReceived(byte[] data)
        {
            Interlocked.Increment(ref _messagesReceived);
            lock (DisposeLock)
            {
                if (IsDisposed) return;

                var ms = new MemoryStream(data);
                var reader = new BinaryReader(ms);

                var messageType = (MessageType) reader.ReadByte();

                if (messageType == MessageType.ReturnValue || messageType == MessageType.ReturnException)
                    ReceiveReply(messageType, reader);
                else if (messageType == MessageType.MethodCall)
                    ReceiveMethodCall(reader);
                else if (messageType == MessageType.Activation)
                    ReceiveActivation(reader);
                else if (messageType == MessageType.Deactivation)
                    ReceiveDeactivation(reader);
            }
        }

        void ReceiveReply(MessageType messageType, BinaryReader reader)
        {
            int msgNumber = reader.ReadInt32();
            object value = DeserializeValue(reader);
            Action<MessageType, object> reply;
            lock (_pendingReplies)
            {
                if (!_pendingReplies.TryGetValue(msgNumber, out reply)) return; // Orphan reply		
                _pendingReplies.Remove(msgNumber);
            }

            reply(messageType, value);
        }

        void ReceiveMethodCall(BinaryReader reader)
        {
            int messageNumber = reader.ReadInt32();
            int objectID = reader.ReadInt32();
            bool awaitRemoteTask = reader.ReadBoolean();
            var method = DeserializeMethod(reader);

            Exception error = null;
            object returnValue = null;
            object[] args = null;
            IProxy proxy = null;

            try
            {
                args = DeserializeArguments(reader, method.GetParameters()).ToArray();
                proxy = FindProxy(objectID);
                if (proxy == null) throw new ObjectDisposedException("Proxy " + objectID + " has been disposed.");
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Task.Factory.StartNew(() =>
            {
                if (error == null)
                    try
                    {
                        var instance = proxy.LocalInstanceUntyped;
                        if (instance == null)
                        {
                            string typeInfo = proxy.ObjectType == null ? "?" : proxy.ObjectType.FullName;
                            error = new ObjectDisposedException(
                                "Proxy " + objectID + " is disposed. Type: " + typeInfo);
                        }
                        else returnValue = method.Invoke(instance, args);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TargetInvocationException) error = ex.InnerException;
                        else error = ex;
                    }

                SendReply(messageNumber, returnValue, error, awaitRemoteTask);
            }, TaskCreationOptions.PreferFairness);
        }

        void ReceiveActivation(BinaryReader reader)
        {
            int messageNumber = reader.ReadInt32();
            object instance = null;
            Exception error = null;
            try
            {
                var type = DeserializeType(reader);
                instance = Activator.CreateInstance(type, true);
                var proxy = (IProxy) Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] {instance, DomainAddress}, null);
                instance = RegisterLocalProxy(proxy);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            SendReply(messageNumber, instance, error, false);
        }

        void SendReply(int messageNumber, object returnValue, Exception error, bool awaitRemoteTask)
        {
            lock (DisposeLock)
            {
                if (IsDisposed) return;
                if (awaitRemoteTask)
                {
                    var returnTask = (Task) returnValue;
                    // The method we're calling is itself asynchronous. Delay sending a reply until the method itself completes.
                    returnTask.ContinueWith(ant => SendReply(messageNumber,
                        ant.IsFaulted ? null : ant.GetUntypedResult(), ant.Exception, false));
                    return;
                }

                var ms = new MemoryStream();
                var writer = new BinaryWriter(ms);
                writer.Write((byte) (error == null ? MessageType.ReturnValue : MessageType.ReturnException));
                writer.Write(messageNumber);
                SerializeValue(writer, error ?? returnValue);
                writer.Flush();
                _outPipe.Write(ms.ToArray());
            }
        }

        void ReceiveDeactivation(BinaryReader reader)
        {
            int objectID = reader.ReadInt32();
            lock (_proxiesLock)
            {
                var proxy = FindProxy(objectID);
                if (proxy == null) return;
                proxy.Disconnect();
            }
        }

        byte[] SerializeMethodCall(Expression expr, int messageNumber, int objectID, bool awaitRemoteTask)
        {
            if (expr == null) throw new ArgumentNullException("expr");

            MethodInfo method;
            IEnumerable<Expression> args = new Expression [0];
            var mc = expr as MethodCallExpression;
            if (mc != null)
            {
                method = mc.Method;
                args = mc.Arguments;
            }
            else
            {
                var me = expr as MemberExpression;
                if (me != null && me.Member is PropertyInfo)
                    method = ((PropertyInfo) me.Member).GetGetMethod();
                else
                    throw new InvalidOperationException("Only method calls and property reads can be serialized");
            }

            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write((byte) MessageType.MethodCall);
            writer.Write(messageNumber);
            writer.Write(objectID);
            writer.Write(awaitRemoteTask);
            SerializeMethod(writer, method);
            SerializeArguments(writer, args.Select(a => GetExprValue(a, true)).ToArray());
            writer.Flush();
            return ms.ToArray();
        }

        void SerializeArguments(BinaryWriter writer, object[] args)
        {
            writer.Write((byte) args.Length);
            foreach (var o in args) SerializeValue(writer, o);
        }

        IEnumerable<object> DeserializeArguments(BinaryReader reader, ParameterInfo[] args)
        {
            byte objectCount = reader.ReadByte();
            for (int i = 0; i < objectCount; i++) yield return DeserializeValue(reader, args[i].ParameterType);
        }

        enum FastTypeCode : byte
        {
            Null,
            False,
            True,
            Byte,
            Char,
            String,
            Int32,
            Proxy,
            Other
        }

        void SerializeValue(BinaryWriter writer, object o)
        {
            if (o == null)
            {
                writer.Write((byte) FastTypeCode.Null);
            }
            else if (o is bool)
            {
                writer.Write((byte) ((bool) o ? FastTypeCode.True : FastTypeCode.False));
            }
            else if (o is byte)
            {
                writer.Write((byte) FastTypeCode.Byte);
                writer.Write((byte) o);
            }
            else if (o is char)
            {
                writer.Write((byte) FastTypeCode.Char);
                writer.Write((char) o);
            }
            else if (o is string)
            {
                writer.Write((byte) FastTypeCode.String);
                writer.Write((string) o);
            }
            else if (o is int)
            {
                writer.Write((byte) FastTypeCode.Int32);
                writer.Write((int) o);
            }
            else if (o is IProxy)
            {
                writer.Write((byte) FastTypeCode.Proxy);
                var proxy = (IProxy) o;
                if (proxy.LocalInstanceUntyped != null) proxy = RegisterLocalProxy(proxy);
                var typeArgType = o.GetType().GetGenericArguments()[0];
                SerializeType(writer, typeArgType);
                SerializeType(writer,
                    proxy.LocalInstanceUntyped == null ? typeArgType : proxy.LocalInstanceUntyped.GetType());
                writer.Write(proxy.ObjectID.Value);
                // The domain address will be zero if created via implicit conversion.
                writer.Write(proxy.DomainAddress == 0 ? DomainAddress : proxy.DomainAddress);
            }
            else
            {
                writer.Write((byte) FastTypeCode.Other);
                writer.Flush();
                new BinaryFormatter().Serialize(writer.BaseStream, o);
            }
        }

        object DeserializeValue(BinaryReader reader, Type expectedType = null)
        {
            var typeCode = (FastTypeCode) reader.ReadByte();
            if (typeCode == FastTypeCode.Null) return null;
            if (typeCode == FastTypeCode.False) return false;
            if (typeCode == FastTypeCode.True) return true;
            if (typeCode == FastTypeCode.Byte) return reader.ReadByte();
            if (typeCode == FastTypeCode.Char) return reader.ReadChar();
            if (typeCode == FastTypeCode.String) return reader.ReadString();
            if (typeCode == FastTypeCode.Int32) return reader.ReadInt32();
            if (typeCode == FastTypeCode.Proxy)
            {
                Type genericType = DeserializeType(reader);
                Type actualType = DeserializeType(reader);
                int objectID = reader.ReadInt32();
                byte domainAddress = reader.ReadByte();
                if (domainAddress == DomainAddress) // We own the real object
                {
                    var proxy = FindProxy(objectID);
                    if (proxy == null)
                        throw new ObjectDisposedException("Cannot deserialize type '" + genericType.Name +
                                                          "' - object has been disposed");

                    // Automatically unmarshal if necessary:
                    if (expectedType != null && expectedType.IsInstanceOfType(proxy.LocalInstanceUntyped))
                        return proxy.LocalInstanceUntyped;

                    return proxy;
                }

                // The other domain owns the object.
                var proxyType = typeof(Proxy<>).MakeGenericType(genericType);
                return Activator.CreateInstance(proxyType, BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new object[]
                    {
                        this,
                        objectID,
                        domainAddress,
                        actualType
                    }, null);
            }

            return new BinaryFormatter().Deserialize(reader.BaseStream);
        }

        void SerializeType(BinaryWriter writer, Type t)
        {
            if (t.Module == _module)
            {
                writer.Write((byte) 1);
                writer.Write(t.MetadataToken);
            }
            else
            {
                writer.Write((byte) 2);
                writer.Write(t.AssemblyQualifiedName);
            }
        }

        Type DeserializeType(BinaryReader reader)
        {
            int b = reader.ReadByte();
            if (b == 1)
                return _module.ResolveType(reader.ReadInt32());
            else
                return Type.GetType(reader.ReadString());
        }

        void SerializeMethod(BinaryWriter writer, MethodInfo mi)
        {
            if (mi.Module == _module)
            {
                writer.Write((byte) 1);
                writer.Write(mi.MetadataToken);
            }
            else
            {
                writer.Write((byte) 2);
                writer.Write(mi.DeclaringType.AssemblyQualifiedName);
                writer.Write(mi.MetadataToken);
            }
        }

        MethodBase DeserializeMethod(BinaryReader reader)
        {
            int b = reader.ReadByte();
            if (b == 1)
                return _module.ResolveMethod(reader.ReadInt32());
            else
                return Type.GetType(reader.ReadString(), true).Module.ResolveMethod(reader.ReadInt32());
        }

        /// <summary>Evalulates an expression tree quickly on the local side without the cost of calling Compile().
        /// This works only with simple method calls and property reads. In other cases, it returns null.</summary>
        public static Func<T, U> FastEvalExpr<T, U>(Expression body)
        {
            // Optimize common cases:
            MethodInfo method;
            IEnumerable<Expression> args = new Expression [0];
            var mc = body as MethodCallExpression;
            if (mc != null)
            {
                method = mc.Method;
                args = mc.Arguments;
            }
            else
            {
                var me = body as MemberExpression;
                if (me != null && me.Member is PropertyInfo)
                    method = ((PropertyInfo) me.Member).GetGetMethod();
                else
                    return null;
            }

            return x =>
            {
                try
                {
                    return (U) method.Invoke(x, args.Select(a => GetExprValue(a, false)).ToArray());
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            };
        }

        static object GetExprValue(Expression expr, bool deferLocalInstanceProperty)
        {
            // Optimize the common simple cases, the first being a simple constant:
            var constant = expr as ConstantExpression;
            if (constant != null) return constant.Value;

            // The second common simple case is accessing a field in a closure:
            var me = expr as MemberExpression;

            if (me != null && me.Member is FieldInfo && me.Expression is ConstantExpression)
                return ((FieldInfo) me.Member).GetValue(((ConstantExpression) me.Expression).Value);

            // If we're referring to the LocalInstance property of the proxy, we need to defer its evaluation
            // until it's deserialized at the other end, as it will likely be null:
            if (deferLocalInstanceProperty && me != null && me.Member is PropertyInfo)
            {
                if (me.Member.Name == "LocalInstance" &&
                    me.Member.ReflectedType.IsGenericType &&
                    me.Member.ReflectedType.GetGenericTypeDefinition() == typeof(Proxy<>))
                {
                    return GetExprValue(me.Expression, true);
                }
            }

            // This will take longer:
            var objectMember = Expression.Convert(expr, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            return getter();
        }
    }


    #region Proxy

    interface IProxy
    {
        /// <summary>Nongeneric version of the LocalInstance </summary>
        object LocalInstanceUntyped { get; }

        FastChannel Channel { get; }
        int? ObjectID { get; }
        byte DomainAddress { get; }
        void Disconnect();
        Type ObjectType { get; }
        bool IsDisconnected { get; }

        /// <summary>Connects the proxy for channel implementors. Used by FastChannel.</summary>
        void RegisterLocal(FastChannel fastChannel, int? objectID, Action onDisconnect);
    }

    /// <summary>
    /// Wraps a reference to an object that potentially lives in another domain. This ensures that cross-domain calls are explicit in the
    /// source code, and allows for a transport other than .NET Remoting (e.g., FastChannel).
    /// </summary>
    class Proxy<TRemote> : IProxy where TRemote : class
    {
        /// <summary>Any reference-type object can be implicitly converted to a proxy. The proxy will become connected
        /// automatically when it's serialized during a remote method call.</summary>
        public static implicit operator Proxy<TRemote>(TRemote instance)
        {
            if (instance == null) return null;
            return new Proxy<TRemote>(instance);
        }

        readonly object _locker = new object();

        /// <summary>The object being Remoted. This is populated only on the local side.</summary>
        public TRemote LocalInstance { get; private set; }

        /// <summary>This is populated if the proxy was obtained via FastChannel instead of Remoting.</summary>
        public FastChannel Channel { get; private set; }

        /// <summary>This is populated if the proxy was obtained via FastChannel instead of Remoting. It uniquely identifies the
        /// object within the channel if the object is connected (has a presence in the other domain).</summary>
        public int? ObjectID { get; private set; }

        /// <summary>Identifies the domain that owns the local instance, when FastChannel is in use.</summary>
        public byte DomainAddress { get; private set; }

        public bool IsDisconnected
        {
            get { return LocalInstance == null && Channel == null; }
        }

        Action _onDisconnect;
        readonly Type _actualObjectType;

        object IProxy.LocalInstanceUntyped
        {
            get { return LocalInstance; }
        }

        /// <summary>The real type of the object being proxied. This may be a subclass or derived implementation of TRemote.</summary>
        public Type ObjectType
        {
            get { return _actualObjectType ?? (LocalInstance != null ? LocalInstance.GetType() : typeof(TRemote)); }
        }

        Proxy(IProxy conversionSource, Action onDisconnect, Type actualObjectType)
        {
            LocalInstance = (TRemote) conversionSource.LocalInstanceUntyped;
            Channel = conversionSource.Channel;
            ObjectID = conversionSource.ObjectID;
            DomainAddress = conversionSource.DomainAddress;
            _onDisconnect = onDisconnect;
            _actualObjectType = actualObjectType;
        }

        public Proxy(TRemote instance)
        {
            LocalInstance = instance;
        }

        // Called via reflection:
        Proxy(TRemote instance, byte domainAddress)
        {
            LocalInstance = instance;
            DomainAddress = domainAddress;
        }

        // Called via reflection:
        Proxy(FastChannel channel, int objectID, byte domainAddress, Type actualInstanceType)
        {
            Channel = channel;
            ObjectID = objectID;
            DomainAddress = domainAddress;
            _actualObjectType = actualInstanceType;
        }

        public void AssertRemote()
        {
            if (LocalInstance != null)
                throw new InvalidOperationException("Object " + LocalInstance.GetType().Name + " is not remote");
        }

        /// <summary>Runs a (void) method on the object being proxied. This works on both the local and remote side.</summary>
        public Task Run(Expression<Action<TRemote>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, object>(remoteMethod.Body);
                    if (fastEval != null) fastEval(li);
                    else remoteMethod.Compile()(li);
                    return Task.FromResult(false);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }

            return SendMethodCall<object>(remoteMethod.Body, false);
        }

        /// <summary>Runs a (void) method on the object being proxied. This works on both the local and remote side.
        /// Use this overload for methods on the other domain that are themselves asynchronous.</summary>
        public Task Run(Expression<Func<TRemote, Task>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, Task>(remoteMethod.Body);
                    if (fastEval != null) return fastEval(li);
                    return remoteMethod.Compile()(li);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }

            return SendMethodCall<object>(remoteMethod.Body, true);
        }

        /// <summary>Runs a non-void method on the object being proxied. This works on both the local and remote side.</summary>
        public Task<TResult> Eval<TResult>(Expression<Func<TRemote, TResult>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, TResult>(remoteMethod.Body);
                    if (fastEval != null) return Task.FromResult(fastEval(li));
                    return Task.FromResult(remoteMethod.Compile()(li));
                }
                catch (Exception ex)
                {
                    return Task.FromException<TResult>(ex);
                }

            return SendMethodCall<TResult>(remoteMethod.Body, false);
        }

        /// <summary>Runs a non-void method on the object being proxied. This works on both the local and remote side.
        /// Use this overload for methods on the other domain that are themselves asynchronous.</summary>
        public Task<TResult> Eval<TResult>(Expression<Func<TRemote, Task<TResult>>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, Task<TResult>>(remoteMethod.Body);
                    if (fastEval != null) return fastEval(li);
                    return remoteMethod.Compile()(li);
                }
                catch (Exception ex)
                {
                    return Task.FromException<TResult>(ex);
                }

            return SendMethodCall<TResult>(remoteMethod.Body, true);
        }

        Task<TResult> SendMethodCall<TResult>(Expression expressionBody, bool awaitRemoteTask)
        {
            FastChannel channel;
            int? objectID;
            lock (_locker)
            {
                if (Channel == null)
                    return Task.FromException<TResult>(new InvalidOperationException(
                        "Channel has been disposed on Proxy<" + typeof(TRemote).Name + "> " + expressionBody));

                channel = Channel;
                objectID = ObjectID;
            }

            return channel.SendMethodCall<TResult>(expressionBody, objectID.Value, awaitRemoteTask);
        }

        /// <summary>This is useful when this.ObjectType is a subclass or derivation of TRemote.</summary>
        public Proxy<TNew> CastTo<TNew>() where TNew : class, TRemote
        {
            if (!typeof(TNew).IsAssignableFrom(ObjectType))
                throw new InvalidCastException("Type '" + ObjectType.FullName + "' cannot be cast to '" +
                                               typeof(TNew).FullName + "'.");

            return new Proxy<TNew>(this, _onDisconnect, _actualObjectType);
        }

        void IProxy.RegisterLocal(FastChannel fastChannel, int? objectID, Action onDisconnect)
        {
            // This is called by FastChannel to connect/register the proxy.
            lock (_locker)
            {
                Channel = fastChannel;
                ObjectID = objectID;
                DomainAddress = fastChannel.DomainAddress;
                _onDisconnect = onDisconnect;
            }
        }

        public void Disconnect()
        {
            Action onDisconnect;
            lock (_locker)
            {
                onDisconnect = _onDisconnect;
                _onDisconnect = null;
            }

            if (onDisconnect != null) onDisconnect();

            // If the remote reference drops away, we should ensure that it gets release on the other domain as well:

            lock (_locker)
            {
                if (Channel == null || LocalInstance != null || ObjectID == null)
                    LocalInstance = null;
                else
                    Channel.InternalDeactivate(ObjectID.Value);

                Channel = null;
                ObjectID = null;
            }
        }

        ~Proxy()
        {
            if (_locker != null)
                try
                {
                    Disconnect();
                }
                catch (ObjectDisposedException)
                {
                }
        }
    }

    #endregion

    #region Pipe

    class SafeMemoryMappedFile : SafeDisposable
    {
        readonly MemoryMappedFile _mmFile;
        readonly MemoryMappedViewAccessor _accessor;
        unsafe byte* _pointer;

        public int Length { get; private set; }

        public MemoryMappedViewAccessor Accessor
        {
            get
            {
                AssertSafe();
                return _accessor;
            }
        }

        public unsafe byte* Pointer
        {
            get
            {
                AssertSafe();
                return _pointer;
            }
        }

        public unsafe SafeMemoryMappedFile(MemoryMappedFile mmFile)
        {
            _mmFile = mmFile;
            _accessor = _mmFile.CreateViewAccessor();
            _pointer = (byte*) _accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            Length = (int) _accessor.Capacity;
        }

        unsafe protected override void DisposeCore()
        {
            base.DisposeCore();
            _accessor.Dispose();
            _mmFile.Dispose();
            _pointer = null;
        }
    }

    abstract class MutexFreePipe : SafeDisposable
    {
        protected const int MinimumBufferSize = 0x10000;
        protected readonly int MessageHeaderLength = sizeof(int);
        protected readonly int StartingOffset = sizeof(int) + sizeof(bool);

        public readonly string Name;
        protected readonly EventWaitHandle NewMessageSignal;
        protected SafeMemoryMappedFile Buffer;
        protected int Offset, Length;

        protected MutexFreePipe(string name, bool createBuffer)
        {
            Name = name;

            var mmFile = createBuffer
                ? MemoryMappedFile.CreateNew(name + ".0", MinimumBufferSize, MemoryMappedFileAccess.ReadWrite)
                : MemoryMappedFile.OpenExisting(name + ".0");

            Buffer = new SafeMemoryMappedFile(mmFile);
            NewMessageSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".signal");

            Length = Buffer.Length;
            Offset = StartingOffset;
        }

        protected override void DisposeCore()
        {
            base.DisposeCore();
            Buffer.Dispose();
            NewMessageSignal.Dispose();
        }
    }

    class OutPipe : MutexFreePipe
    {
        int _messageNumber;
        int _bufferCount;
        readonly List<SafeMemoryMappedFile> _oldBuffers = new List<SafeMemoryMappedFile>();
        public int PendingBuffers => _oldBuffers.Count;

        public OutPipe(string name, bool createBuffer) : base(name, createBuffer)
        {
        }

        public unsafe void Write(byte[] data)
        {
            lock (DisposeLock) // If there are multiple threads, write just one message at a time
            {
                AssertSafe();
                if (data.Length > Length - Offset - 8)
                {
                    // Not enough space left in the shared memory buffer to write the message.
                    WriteContinuation(data.Length);
                }

                WriteMessage(data);
                NewMessageSignal.Set(); // Signal reader that a message is available
            }
        }

        unsafe void WriteMessage(byte[] block)
        {
            byte* ptr = Buffer.Pointer;
            byte* offsetPointer = ptr + Offset;

            var msgPointer = (int*) offsetPointer;
            *msgPointer = block.Length;

            Offset += MessageHeaderLength;
            offsetPointer += MessageHeaderLength;

            if (block != null && block.Length > 0)
            {
                //MMF.Accessor.WriteArray (Offset, block, 0, block.Length);   // Horribly slow. No. No. No.
                Marshal.Copy(block, 0, new IntPtr(offsetPointer), block.Length);
                Offset += block.Length;
            }

            // Write the latest message number to the start of the buffer:
            int* iptr = (int*) ptr;
            *iptr = ++_messageNumber;
        }

        void WriteContinuation(int messageSize)
        {
            // First, allocate a new buffer:		
            string newName = Name + "." + ++_bufferCount;
            int newLength = Math.Max(messageSize * 10, MinimumBufferSize);
            var newFile =
                new SafeMemoryMappedFile(MemoryMappedFile.CreateNew(newName, newLength,
                    MemoryMappedFileAccess.ReadWrite));


            // Write a message to the old buffer indicating the address of the new buffer:
            WriteMessage(new byte [0]);

            // Keep the old buffer alive until the reader has indicated that it's seen it:
            _oldBuffers.Add(Buffer);

            // Make the new buffer current:
            Buffer = newFile;
            Length = newFile.Length;
            Offset = StartingOffset;

            // Release old buffers that have been read:	
            foreach (var buffer in _oldBuffers.Take(_oldBuffers.Count - 1).ToArray())
                lock (buffer.DisposeLock)
                    if (!buffer.IsDisposed && buffer.Accessor.ReadBoolean(4))
                    {
                        _oldBuffers.Remove(buffer);
                        buffer.Dispose();

                    }
        }

        protected override void DisposeCore()
        {
            base.DisposeCore();
            foreach (var buffer in _oldBuffers) buffer.Dispose();
        }
    }

    class InPipe : MutexFreePipe
    {
        int _lastMessageProcessed;
        int _bufferCount;

        readonly Action<byte[]> _onMessage;

        public InPipe(string name, bool createBuffer, Action<byte[]> onMessage) : base(name, createBuffer)
        {
            _onMessage = onMessage;
            new Thread(Go).Start();
        }

        void Go()
        {
            int spinCycles = 0;
            while (true)
            {
                int? latestMessageID = GetLatestMessageID();
                if (latestMessageID == null) return; // We've been disposed.

                if (latestMessageID > _lastMessageProcessed)
                {
                    Thread.MemoryBarrier(); // We need this because of lock-free implementation						
                    byte[] msg = GetNextMessage();
                    if (msg == null) return;
                    if (msg.Length > 0 && _onMessage != null)
                        _onMessage(msg); // Zero-length msg will be a buffer continuation 
                    spinCycles = 1000;
                }

                if (spinCycles == 0)
                {
                    NewMessageSignal.WaitOne();
                    if (IsDisposed) return;
                }
                else
                {
                    Thread.MemoryBarrier(); // We need this because of lock-free implementation		
                    spinCycles--;
                }
            }
        }

        unsafe int? GetLatestMessageID()
        {
            lock (DisposeLock)
            lock (Buffer.DisposeLock)
                return IsDisposed || Buffer.IsDisposed ? (int?) null : *((int*) Buffer.Pointer);
        }

        unsafe byte[] GetNextMessage()
        {
            _lastMessageProcessed++;

            lock (DisposeLock)
            {
                if (IsDisposed) return null;

                lock (Buffer.DisposeLock)
                {
                    if (Buffer.IsDisposed) return null;

                    byte* offsetPointer = Buffer.Pointer + Offset;
                    var msgPointer = (int*) offsetPointer;

                    int msgLength = *msgPointer;

                    Offset += MessageHeaderLength;
                    offsetPointer += MessageHeaderLength;

                    if (msgLength == 0)
                    {
                        Buffer.Accessor.Write(4, true); // Signal that we no longer need file				
                        Buffer.Dispose();
                        string newName = Name + "." + ++_bufferCount;
                        Buffer = new SafeMemoryMappedFile(MemoryMappedFile.OpenExisting(newName));
                        Offset = StartingOffset;
                        return new byte [0];
                    }

                    Offset += msgLength;

                    //MMF.Accessor.ReadArray (Offset, msg, 0, msg.Length);    // too slow			
                    var msg = new byte [msgLength];
                    Marshal.Copy(new IntPtr(offsetPointer), msg, 0, msg.Length);
                    return msg;
                }
            }
        }

        protected override void DisposeCore()
        {
            NewMessageSignal.Set();
            base.DisposeCore();
        }
    }

    #endregion

    #region SafeDisposable

    public class SafeDisposable : IDisposable
    {
        public object DisposeLock = new object();
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            lock (DisposeLock)
                if (!IsDisposed)
                {
                    IsDisposed = true;
                    DisposeCore();
                }
        }

        protected virtual void DisposeCore()
        {
        }

        public void AssertSafe()
        {
            lock (DisposeLock)
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().Name + " has been disposed");
        }
    }

    #endregion

    #region Extensions

    static class Extensions
    {
        public static object GetUntypedResult(this Task t)
        {
            if (!t.GetType().IsGenericType ||
                t.GetType().GetGenericArguments().First().Name == "VoidTaskResult") return null;
            //return ((dynamic) t).Result;
            return t;
        }
    }

    #endregion
}
