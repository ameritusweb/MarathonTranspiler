# Marathon CSharp Transpiler Developer Documentation

## Overview

The Marathon CSharp Transpiler converts runtime-first Marathon code into standard C# classes, methods, and control structures. It supports Marathon's innovative flow and loop syntax, allowing developers to write code in execution order rather than class-based structure.

## Architecture

The CSharp Transpiler is implemented using a partial class pattern, with specialized handlers for different annotation types:

```
MarathonTranspiler.Transpilers.CSharp
├── CSharpTranspiler.cs           # Main transpiler class
├── CSharpConfig.cs               # Configuration options
└── Partials/                     # Specialized handlers
    ├── CSharpTranspiler.Assert.cs  # @assert handling
    ├── CSharpTranspiler.Flow.cs    # @flow handling
    ├── CSharpTranspiler.More.cs    # @more handling
    ├── CSharpTranspiler.Run.cs     # @run handling
    └── CSharpTranspiler.VarInit.cs # @varInit handling
```

The transpiler inherits from `MarathonTranspilerBase` which provides common functionality for all transpilers.

## Key Components

### CSharpTranspiler

The main transpiler class handles the overall code generation process, producing C# code from the processed annotations. It manages:

- Class generation
- Method generation
- Program class generation
- Test generation (if assertions are present)

### CSharpConfig

Configuration options for the C# transpiler, including:

- `TestFramework`: The test framework to use (xUnit or NUnit)

### Flow Processing

`CSharpTranspiler.Flow.cs` handles the innovative flow syntax, including:

- Named flow blocks with `@flow(name="flowName")`
- Control flow syntax with ``@keyword {flowName}``
- Direct flow references with `{flowName}`
- Advanced loop syntax with transformation and filtering

## Annotation Processing

### @varInit

Processes variable initializations, handles both field and property declarations:

```csharp
@varInit(className="Counter", type="int")
this.count = 0;

// Generates:
public class Counter {
    public int count { get; set; }
    
    public Counter() {
        this.count = 0;
    }
}
```

### @run

Processes method declarations and implementations:

```csharp
@run(className="Counter", functionName="increment")
this.count++;

// Generates:
public class Counter {
    // ...
    public void increment() {
        this.count++;
    }
}
```

### @assert

Processes assertions, generating test methods:

```csharp
@assert(className="Counter", condition="this.count >= 0")
"Count should never be negative"

// Generates:
public class CounterTests {
    [Fact] // or [Test] for NUnit
    public void TestAssertions() {
        var instance = new Counter();
        Assert.True(instance.count >= 0, "Count should never be negative");
    }
}
```

### @more

Adds additional code to an existing method:

```csharp
@more(id="method1")
Console.WriteLine("Additional code");

// Added to the method with id="method1"
```

### @flow

Defines a named block of code that can be referenced by other blocks:

```csharp
@flow(name="validateInput")
if (input == null) throw new ArgumentNullException(nameof(input));
if (input.Length == 0) throw new ArgumentException("Input cannot be empty");
```

## Flow Syntax

### Direct Flow References

```csharp
@run(className="OrderProcessor", functionName="ProcessOrder")
{ValidateOrder}
{CalculateTotal}
{ProcessPayment}
return true;
```

The transpiler replaces each `{flowName}` reference with the corresponding flow's code.

### Control Flow Syntax

```csharp
@run(className="DataProcessor", functionName="ProcessData")
``@if (data.Length > 1000) {HandleLargeData}
``@else {HandleSmallData}
```

The transpiler generates appropriate if/else statements with the referenced flow's code.

### Loop Syntax

The transpiler supports advanced loop syntax with transformation and filtering:

```csharp
``@loop [item:collection] {ProcessItem}               // Basic iteration
``@loop [i=1:10] {CountUp}                           // Numeric range (inclusive)
``@loop [t:t.ToUpper():myStrings] {ProcessUppercase}  // Transformation
``@loop [x:x > 5:numbers] {ProcessLargeNumbers}       // Filtering
```

These are converted to appropriate C# foreach loops, potentially with LINQ Where/Select operations.

## Code Generation

The transpiler generates several types of C# code:

1. **Classes**: One class per `className` used in annotations
2. **Methods**: Within classes, methods defined by `@run` and other annotations
3. **Properties**: Class properties defined by `@varInit`
4. **Fields**: Class fields for other variable declarations
5. **Test Classes**: For classes with assertions
6. **Program Class**: A standard Program class with Main method

## Example: Complete Transformation

### Marathon Code:

```csharp
@varInit(className="Counter", type="int")
this.count = 0;

@run(className="Counter", functionName="increment")
this.count++;
Console.WriteLine($"Count: {this.count}");

@run(className="Counter", functionName="process")
``@loop [i=1:5] {CountUp}

@flow(name="CountUp")
this.increment();
```

### Generated C# Code:

```csharp
public class Counter {
    public int count { get; set; }
    
    public Counter() {
        this.count = 0;
    }
    
    public void increment() {
        this.count++;
        Console.WriteLine($"Count: {this.count}");
    }
    
    public void process() {
        for (int i = 1; i <= 5; i++) {
            this.increment();
        }
    }
}

public class Program {
    public static void Main(string[] args) {
        Counter counter = new Counter();
        counter.process();
    }
}
```

## Implementation Details

### Flow Storage and Resolution

Flows are stored in a dictionary keyed by name:

```csharp
private readonly Dictionary<string, List<string>> _flows = new();
```

When a flow reference is encountered, the transpiler looks up the flow's code and inserts it at the reference point.

### Nested Flow Processing

The transpiler handles nested flows by recursively processing flow code:

```csharp
var flowCode = new List<string>(_flows[flowName]);
var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
```

### Advanced Loop Expressions

Loop expressions are parsed and converted to appropriate C# code:

- Basic iteration: `foreach (var item in collection) { ... }`
- Numeric range: `for (int i = 1; i <= 10; i++) { ... }`
- Filtering: `foreach (var x in numbers.Where(x => x > 5)) { ... }`
- Transformation: `foreach (var t in myStrings.Select(x => x.ToUpper())) { ... }`

## Extending the Transpiler

To add new annotation types or syntax features:

1. Create a new partial class file in the `Partials` directory
2. Implement the processing method (e.g., `ProcessNewAnnotation`)
3. Update `ProcessBlock` in `MarathonTranspilerBase` to call your new method

## Best Practices

1. **Use Descriptive Flow Names**: Flow names should clearly indicate their purpose
2. **Keep Flows Focused**: Each flow should have a single responsibility
3. **Validate Flow References**: Ensure all referenced flows are defined
4. **Test Generated Code**: Verify that the generated code compiles and runs correctly

## Troubleshooting

### Common Issues

1. **Flow Not Found**: Check that the flow name exists and matches exactly (case-sensitive)
2. **Invalid Loop Syntax**: Verify that loop expressions follow the correct format
3. **Compilation Errors**: Check for syntax errors in the generated code
4. **Runtime Errors**: Verify that all referenced variables are properly defined

### Debugging Tips

1. Inspect the dictionary of flows (`_flows`) to verify flow definitions
2. Check the processed code lists before they're added to methods
3. Validate that flow references are correctly replaced

## Advanced Usage

### Custom Test Framework Integration

The transpiler supports both xUnit and NUnit test frameworks. To configure:

```csharp
var config = new CSharpConfig { TestFramework = "nunit" };
var transpiler = new CSharpTranspiler(config);
```

### Flow Validation

The transpiler includes validation for flow references, ensuring that all referenced flows are defined:

```csharp
var validationErrors = FlowValidator.ValidateFlowReferences(annotatedCodes);
if (validationErrors.Any())
{
    // Handle validation errors
}
```

## Performance Considerations

1. **Flow Resolution**: Resolving nested flows can be expensive for deep hierarchies
2. **Loop Processing**: Complex loop expressions with multiple operations may impact performance
3. **Code Generation**: Large projects may generate substantial amounts of code

## Conclusion

The Marathon CSharp Transpiler provides a powerful way to write C# code in a runtime-first manner, focusing on execution flow rather than static structure. The innovative flow and loop syntax makes complex code more readable and maintainable.

By following this documentation, developers can effectively use, extend, and troubleshoot the CSharp transpiler for Marathon.