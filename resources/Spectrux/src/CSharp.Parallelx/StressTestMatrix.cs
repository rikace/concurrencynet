using System;

namespace CSharp.Parallelx
{
   
    public class Matrix : IEquatable<Matrix>
    {
        #region Fields

        public const int StrassenMinElementsCount = 9 << 12;

        public static readonly int StrassenMinLength = (int)Math.Sqrt(StrassenMinElementsCount);

        private int[,] values;

        #endregion

        #region Properties

        /// <summary>
        /// The order of the square matrix.
        /// </summary>
        public int Length { get; private set; }

        public int ElementsCount { get; private set; }

        public int this[int i, int j]
        {
            get { return this.values[i, j]; }
            private set { this.values[i, j] = value; }
        }

        #endregion

        #region Constructors

        public Matrix(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException("length", "The length of the matrix must be positive.");

            this.Length = length;
            this.ElementsCount = length * length;

            this.values = new int[length, length];
        }

        #endregion

        #region Arithmetic Static Methods

        public static Matrix Add(Matrix m1, Matrix m2)
        {
            VerifyCompatability(m1, m2);

            int length = m1.Length;
            int[,] m1Values = m1.values;
            int[,] m2Values = m2.values;

            Matrix mRes = new Matrix(length);
            int[,] mResValues = mRes.values;

            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                    mResValues[i, j] = m1Values[i, j] + m2Values[i, j];

            return mRes;
        }

        public static Matrix Subtract(Matrix m1, Matrix m2)
        {
            VerifyCompatability(m1, m2);

            int length = m1.Length;
            int[,] m1Values = m1.values;
            int[,] m2Values = m2.values;

            Matrix mRes = new Matrix(length);
            int[,] mResValues = mRes.values;

            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                    mResValues[i, j] = m1Values[i, j] - m2Values[i, j];

            return mRes;
        }

        public static Matrix MultiplyNaive(Matrix m1, Matrix m2)
        {
            VerifyCompatability(m1, m2);

            int length = m1.Length;
            int[,] m1Values = m1.values;
            int[,] m2Values = m2.values;

            Matrix mRes = new Matrix(length);
            int[,] mResValues = mRes.values;

            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                {
                    int temp = 0;

                    for (int k = 0; k < length; ++k)
                        temp += m1Values[i, k] * m2Values[k, j];

                    mResValues[i, j] = temp;
                }

            return mRes;
        }

        public static Matrix MultiplyStrassen(Matrix m1, Matrix m2)
        {
            VerifyCompatability(m1, m2);

            if (m1.ElementsCount < StrassenMinElementsCount)
                return MultiplyNaive(m1, m2);

            MatrixPair[] mps = Matrix.StrassenSplit(m1, m2);
            Matrix[] ms = mps.SelectArray(Matrix.MultiplyStrassen);
            Matrix mRes = Matrix.StrassenMerge(ms);
            return mRes;
        }

        public static Matrix MultiplyStrassen(MatrixPair mp)
        {
            return MultiplyStrassen(mp.A, mp.B);
        }

        public static bool Equals(Matrix m1, Matrix m2)
        {
            if (object.ReferenceEquals(m1, m2))
                return true;

            if (object.ReferenceEquals(m1, null) || object.ReferenceEquals(m2, null))
                return false;

            if (m1.Length != m2.Length)
                return false;

            int length = m1.Length;
            int[,] m1Values = m1.values;
            int[,] m2Values = m2.values;

            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                    if (m1Values[i, j] != m2Values[i, j])
                        return false;

            return true;
        }
        
        #endregion

        #region Strassen Static Methods

        public static MatrixPair[] StrassenSplit(MatrixPair mp)
        {
            return Matrix.StrassenSplit(mp.A, mp.B);
        }

        public static MatrixPair[] StrassenSplit(Matrix a, Matrix b)
        {
            Matrix[,] As = a.Split(2);
            Matrix[,] Bs = b.Split(2);

            MatrixPair[] Ms = new MatrixPair[]
            {
                new MatrixPair(As[0,0] + As[1,1], Bs[0,0] + Bs[1,1]),
                new MatrixPair(As[1,0] + As[1,1], Bs[0,0]),
                new MatrixPair(As[0,0], Bs[0,1] - Bs[1,1]),
                new MatrixPair(As[1,1], Bs[1,0] - Bs[0,0]),
                new MatrixPair(As[0,0] + As[0,1], Bs[1,1]),
                new MatrixPair(As[1,0] - As[0,0], Bs[0,0] + Bs[0,1]),
                new MatrixPair(As[0,1] - As[1,1], Bs[1,0] + Bs[1,1]),
            };

            return Ms;
        }

        public static Matrix StrassenMerge(Matrix[] Ms)
        {
            Matrix[,] Cs = new Matrix[2, 2];
            Cs[0, 0] = Ms[0] + Ms[3] - Ms[4] + Ms[6];
            Cs[0, 1] = Ms[2] + Ms[4];
            Cs[1, 0] = Ms[1] + Ms[3];
            Cs[1, 1] = Ms[0] - Ms[1] + Ms[2] + Ms[5];
            Matrix C = Matrix.Merge(Cs);
            return C;
        }

        #endregion

        #region Equality Instance Methods

        public bool Equals(Matrix other)
        {
            return Matrix.Equals(this, other);
        }

        public override bool Equals(object other)
        {
            return Matrix.Equals(this, other as Matrix);
        }

        public override int GetHashCode()
        {
            return this.values.GetHashCode();
        }

        #endregion

        #region Operator Overloads

        public static Matrix operator +(Matrix m1, Matrix m2)
        {
            return Matrix.Add(m1, m2);
        }

        public static Matrix operator -(Matrix m1, Matrix m2)
        {
            return Matrix.Subtract(m1, m2);
        }

        public static Matrix operator *(Matrix m1, Matrix m2)
        {
            return Matrix.MultiplyStrassen(m1, m2);
        }

        public static bool operator ==(Matrix m1, Matrix m2)
        {
            return Matrix.Equals(m1, m2);
        }

        public static bool operator !=(Matrix m1, Matrix m2)
        {
            return !(m1 == m2);
        }

        #endregion

        #region Split & Merge Methods

        /// <summary>
        /// Splits the specified matrix into a collection of 
        /// <paramref name="partsCount"/> by <paramref name="partsCount"/> sub-matrices.
        /// </summary>
        public Matrix[,] Split(int partsCount)
        {
            int remainder;
            int partLength = Math.DivRem(this.Length, partsCount, out remainder);
            if (remainder != 0)
                throw new ArgumentException(string.Format(
                    "Current matrix has length {0}, which cannot be split exactly into {1} parts.",
                    this.Length, partsCount));

            int[,] mSourceValues = this.values;
            
            Matrix[,] mParts = new Matrix[partsCount, partsCount];

            int xOffset = 0;
            for (int x = 0; x < partsCount; ++x)
            {
                int yOffset = 0;
                for (int y = 0; y < partsCount; ++y)
                {
                    Matrix mPart = new Matrix(partLength);
                    int[,] mPartValues = mPart.values;

                    for (int ii = 0; ii < partLength; ++ii)
                        for (int jj = 0; jj < partLength; ++jj)
                            mPartValues[ii, jj] = mSourceValues[ii + xOffset, jj + yOffset];

                    mParts[x, y] = mPart;

                    yOffset += partLength;
                }

                xOffset += partLength;
            }

            return mParts;
        }

        /// <summary>
        /// Merges the specified collection of sub-matrices into a single matrix.
        /// </summary>
        public static Matrix Merge(Matrix[,] mParts)
        {
            VerifyCompatability(mParts);

            int partsCount = mParts.GetLength(0);
            int partLength = mParts[0, 0].Length;
            int resultLength = partLength * partsCount;

            Matrix mResult = new Matrix(resultLength);
            int[,] mResultValues = mResult.values;
            
            int xOffset = 0;
            for (int x = 0; x < partsCount; ++x)
            {
                int yOffset = 0;
                for (int y = 0; y < partsCount; ++y)
                {
                    Matrix mPart = mParts[x, y];
                    int[,] mPartValues = mPart.values;

                    for (int ii = 0; ii < partLength; ++ii)
                        for (int jj = 0; jj < partLength; ++jj)
                            mResultValues[ii + xOffset, jj + yOffset] = mPartValues[ii, jj];

                    yOffset += partLength;
                }

                xOffset += partLength;
            }

            return mResult;
        }

        #endregion

        #region Compatability Methods

        public static void VerifyCompatability(Matrix m1, Matrix m2)
        {
            if (m1.Length != m2.Length)
                throw new ArgumentException("The specified matrices have different lengths.");
        }

        public static void VerifyCompatability(Matrix[,] ms)
        {
            int xLength = ms.GetLength(0);
            int yLength = ms.GetLength(1);

            if (xLength == 0)
                throw new ArgumentException("The specified array of matrices is empty.");
            if (xLength != yLength)
                throw new ArgumentException("The specified array of matrices is not square.");

            int mLength = ms[0, 0].Length;

            for (int x = 0; x < xLength; ++x)
                for (int y = 0; y < yLength; ++y)
                    if (ms[x, y].Length != mLength)
                        throw new ArgumentException("The specified matrices have different lengths.");
        }

        #endregion

        #region Generation Methods

        public static Matrix GenerateRandom(int length)
        {
            Random random = new Random();
            return GenerateRandom(length, random);
        }
            
        public static Matrix GenerateRandom(int length, Random random)
        {
            Matrix m = new Matrix(length);
            int[,] mValues = m.values;

            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                    mValues[i, j] = random.Next(1024);

            return m;
        }

        #endregion
    }
    public class MatrixPair
    {
        #region Properties

        public Matrix A { get; private set; }
        public Matrix B { get; private set; }
        
        #endregion

        #region Constructors

        public MatrixPair(Matrix a, Matrix b)
        {
            Matrix.VerifyCompatability(a, b);

            this.A = a;
            this.B = b;
        }

        #endregion

        #region Generation Methods

        public static MatrixPair GenerateRandom(int length)
        {
            Random random = new Random();
            Matrix a = Matrix.GenerateRandom(length, random);
            Matrix b = Matrix.GenerateRandom(length, random);
            return new MatrixPair(a, b);
        }

        #endregion
    }
}
