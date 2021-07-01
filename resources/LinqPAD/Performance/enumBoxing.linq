<Query Kind="Program" />

void Main()
{
	var iterations = 1000_000_000;
	
	Animal cat = Animal.Cat;
	Animal dog = Animal.Dog;

	
	// when enum is ised as key in a lookup/dictionary so the hash-code is called multiple time 
	
	var enValues = Enum.GetValues(typeof(Animal)).Cast<Animal>();
	var sum = 0;
	for (int i = 0; i < iterations; i++)
	{
		//sum += ((int)cat).GetHashCode();  // ~3 sec
		//sum += cat.GetHashCode();  // ~12
		sum += (int)enValues.Max();
		
		
		
	}
	
	sum.Dump();
	
}

enum Animal  
{
	Cat,
	Dog
}

// ldc.i4 = Push num of type int32 onto the stack
// stloc = pop a value from the stack 
// ldloca = Load address of local variable

