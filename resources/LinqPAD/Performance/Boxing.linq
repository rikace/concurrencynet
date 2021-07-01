<Query Kind="Statements" />

int iterations = 50_000_000;

int a = 40;
var builder = new StringBuilder();

for (int i = 0; i < iterations; i++)
{
	//string text = string.Format("{0} + {1} = {2}", a, i, (a + i));
	string text = string.Format("{0} + {1} = {2}", a.ToString(), i.ToString(), (a + i).ToString());
	//string text = $"{a} + {i} = {a + i}";

	builder.Append(text);
	if(i%1000==0)
	builder.Clear();
}

builder.ToString().Length.Dump();
