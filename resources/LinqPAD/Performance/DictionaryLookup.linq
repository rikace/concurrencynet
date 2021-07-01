<Query Kind="Program" />

void Main()
{
	var keys = Enumerable.Range(0,10000000).ToList();
	var d = keys.ToDictionary(k => k, v => Guid.NewGuid());
	
	var sw = System.Diagnostics.Stopwatch.StartNew();
	foreach (var key in keys)
	{
		Get1(d,key);
	}
	sw.ElapsedMilliseconds.Dump();

	sw.Restart();
	foreach (var key in keys)
	{
		Get2(d, key);
	}
	sw.ElapsedMilliseconds.Dump();
}

public static Guid Get1(IDictionary<int, Guid> dictionary, int key)
{
	if (dictionary.ContainsKey(key))
		return dictionary[key];
	return Guid.Empty;
}

public static Guid Get2(IDictionary<int, Guid> dictionary, int key)
{
	Guid result;
	dictionary.TryGetValue(key, out result);
	return result;
}