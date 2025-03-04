# Marathon Transpiler
Marathon is a Runtime-First code transpiler that lets you write code in execution order, then automatically generates the necessary class structures and boilerplate. It's particularly useful for event-driven, distributed, and parallel systems where execution flow is important to understand.

## Why Runtime-First?
Traditional programming starts with static structures (classes, methods) before defining execution flow. This can make it harder to understand how code actually runs, especially in:

- Distributed systems (like Microsoft Orleans)
- UI frameworks (React, WPF)
- Game engines (Unity)
- Scientific computing (Python with NumPy/SymPy)

Marathon lets you write code in the order it executes, then handles the structure for you.

## Key Features

- **Runtime-First Development**: Write code in the order it executes
- **Multiple Target Frameworks**: Generate code for C#, React, React Redux, ASP.NET, Orleans, Unity, Python, and WPF
- **VS Code Integration**: Full language server support with autocompletion, diagnostics, hover information, and semantic highlighting
- **Seamless Testing**: Integrate assertions directly into your runtime flow and automatically generate unit tests
- **Advanced Flow Control**: Use powerful flow syntax for complex control patterns

## New Features

### Advanced Flow System
- Named flow blocks with `@flow(name="flowName")`
- Control flow syntax with `--@keyword {flowName}`
- Direct flow references with `{flowName}`
- Conditional branching with `--@if (condition) {flowName} and `--@else {flowName}`
- Switch/case handling with `--@switch (expression) {SwitchFlowName}` and `--@case value {CaseFlowName}`
- Advanced loop syntax with transformation and filtering:
  ```
  --@loop [item:collection] {ProcessItem}               // Basic iteration
  --@loop [i=1:10] {CountUp}                           // Numeric range (inclusive)
  --@loop [t:t.ToUpper():myStrings] {ProcessUppercase}  // Transformation
  --@loop [x:x > 5:numbers] {ProcessLargeNumbers}       // Filtering
  ```

### Enhanced VS Code Extension
- Smart autocompletion with context-aware suggestions
- Real-time diagnostics for annotation validation
- Semantic highlighting for better code readability
- Go-to-definition support for navigating between related code blocks
- Hover information for annotation documentation

### Static Method Inlining
- Inline static methods with --@ClassName.MethodName() syntax
- Automatic dependency resolution and inclusion
- Cross-language support for both C# and JavaScript/TypeScript methods

### Full-Stack Web Development Support
- Generate both frontend (React/Redux) and backend (ASP.NET) code from a single source
- Model relationship handling for entity frameworks
- Automatic REST API endpoint generation
- Redux state management with normalized selectors

### Advanced Testing Integration
- Runtime-First TDD with `@assert` annotations embedded in the execution flow
- Automatic test case generation for multiple frameworks (xUnit, NUnit, Jest)
- Framework-specific test optimizations

### Orleans Integration
- First-class support for Microsoft Orleans distributed framework
- Automatic grain interface and implementation generation
- Stateful grain management

## Getting Started

### Installation

```bash
# Install the CLI tool
dotnet tool install --global Marathon.CLI

# Install the VS Code extension
cd vscode\extension\marathon-ext
code --install-extension marathon-ext-0.0.5.vsix
```

### Basic Usage

Create a `mrtconfig.json` file in your project root:

```json
{
  "transpilerOptions": {
    "target": "csharp",
    "csharp": {
      "testFramework": "xunit"
    }
  },
  "include": ["**/*.mrt"],
  "exclude": ["**/node_modules/**"],
  "rootDir": "./src"
}
```

Create a `.mrt` file with runtime-first code:

```csharp
@varInit(className="Counter", type="int")
this.count = 0;

@run(className="Counter", functionName="increment")
this.count++;
Console.WriteLine($"Count: {this.count}");

@assert(className="Counter", condition="this.count > 0")
"Increment should increase count"
```

Run the transpiler using the CLI:

```bash
# Basic transpilation
marathon transpile

# Watch for changes and transpile automatically
marathon watch

# List all available inlineable functions
marathon list

# Initialize a new project
marathon init --target csharp
```

The transpiler will generate properly structured code in the target language.

# **Runtime-First Software Development: A Paradigm Shift in Code Design and Test-Driven Development**

## **Introduction**
Software development has long been driven by **static structure**—where developers first define classes, functions, and modules before determining how execution flows at runtime. This traditional **compile-time-first approach** is deeply ingrained in modern programming methodologies. However, it introduces challenges, particularly when dealing with **highly dynamic, event-driven, and parallel systems**, where execution order is not strictly linear.

To address these issues, a new paradigm emerges: **Runtime-First Software Development**. This approach prioritizes **execution flow over static structure**, allowing developers to define code in the **order it will be executed**. A powerful extension of this idea is **Runtime-First Test-Driven Development (TDD)**, which integrates correctness expectations directly into the execution model, making testing more intuitive and reducing cognitive load.

This essay explores the **Runtime-First paradigm**, its **advantages over traditional software design**, and its **transformative impact on TDD**.

---

## **The Runtime-First Paradigm: Designing for Execution**
Traditional programming methodologies focus on defining **static structure** first, typically by:
- Organizing **functions, classes, and modules** in a way that makes sense to the developer.
- Defining **variables and data models** before thinking about how they change over time.
- Writing **event handlers and execution logic separately**, requiring careful orchestration to ensure correct behavior.

However, in real-world applications, especially in **UI development, game engines, simulations, and distributed systems**, execution is not always predictable or strictly sequential. These systems often require:
- **Dynamic event handling** (e.g., key presses, network responses, UI interactions).
- **Non-linear execution flows** (e.g., user-driven actions, AI decision-making, game loops).
- **Parallel and concurrent execution** (e.g., multi-threading, async operations, real-time processing).

### **How Runtime-First Programming Works**
Instead of writing code in a **structure-first** manner, the developer **annotates execution flow**, allowing a **transpiler (or inference engine)** to generate compilable, optimized code. This ensures:
1. **Execution order is explicitly defined** in the source code.
2. **Dependencies are inferred automatically**, reducing manual setup.
3. **Parallel and event-driven behavior is handled naturally**.
4. **State management is streamlined**, reducing unnecessary boilerplate.

Consider a simple example of a **counter application** in the runtime-first model:

```typescript
@varInit(className="Counter", type="number")
this.count = 0;

@run(className="Counter", functionName="increment")
this.count++;

@onEvent(event="buttonClick", target="incrementButton")
this.increment();

@onEvent(event="keyPress", key="ArrowUp")
this.increment();
```

The transpiler then converts this into standard React/JavaScript code:

```typescript
import { useState } from "react";

function Counter() {
  const [count, setCount] = useState(0);
  const increment = () => setCount(prev => prev + 1);

  return (
    <div>
      <p>Count: {count}</p>
      <button id="incrementButton" onClick={increment}>Increment</button>
    </div>
  );
}

document.addEventListener("keydown", (event) => {
  if (event.key === "ArrowUp") {
    document.getElementById("incrementButton")?.click();
  }
});

export default Counter;
```

This approach **mirrors how execution actually happens** instead of forcing the developer to define structure first.

## **Runtime-First TDD: Making Test-Driven Development More Intuitive**
One of the most groundbreaking applications of the **Runtime-First paradigm** is in **Test-Driven Development (TDD)**. Traditional TDD follows these steps:
1. Write a **failing test case** first.
2. Implement code to **make the test pass**.
3. Refactor and repeat.

While this process enforces correctness, it has a **cognitive cost**:
- Writing tests that **fail on purpose** feels counterintuitive.
- Developers must **think about testing separately** from implementation.
- Tests can **drift out of sync** with the actual execution flow.

### **How Runtime-First TDD Works**
Instead of writing separate tests, the **developer annotates expected runtime behavior** using `@assert` statements. These:
- **Define correctness conditions** directly within the execution flow.
- **Automatically generate unit tests**, eliminating redundant test-writing.
- **Ensure tests always match runtime behavior**, reducing maintenance effort.

#### **Example: A Shopping Cart with Runtime-First TDD**
Consider an online shopping cart where:
- Clicking **"Add to Cart"** increases the item count.
- Clicking **"Checkout"** resets the cart.
- The **cart should never have a negative count**.

#### **Runtime-First Annotations**
```typescript
@varInit(className="Cart", type="number")
this.items = 0;

@run(className="Cart", functionName="addItem")
this.items++;

@run(className="Cart", functionName="checkout")
this.items = 0;

@onEvent(event="buttonClick", target="addButton")
this.addItem();

@onEvent(event="buttonClick", target="checkoutButton")
this.checkout();

@assert(className="Cart", condition="this.items === 0")
"Cart should start empty";

@assert(className="Cart", action="addItem", condition="this.items > 0")
"Adding an item should increase count";

@assert(className="Cart", action="checkout", condition="this.items === 0")
"Checking out should empty the cart";

@assert(className="Cart", condition="this.items >= 0")
"Cart should never have a negative count";
```

## Generated Test Cases (Jest)

```typescript
describe("Cart Component", () => {
  test("Cart should start empty", () => {
    render(<Cart />);
    expect(screen.getByText(/Items in Cart: 0/i)).toBeInTheDocument();
  });

  test("Adding an item should increase count", () => {
    render(<Cart />);
    fireEvent.click(screen.getByText(/Add to Cart/i));
    expect(screen.getByText(/Items in Cart: 1/i)).toBeInTheDocument();
  });

  test("Checking out should empty the cart", () => {
    render(<Cart />);
    fireEvent.click(screen.getByText(/Add to Cart/i));
    fireEvent.click(screen.getByText(/Checkout/i));
    expect(screen.getByText(/Items in Cart: 0/i)).toBeInTheDocument();
  });

  test("Cart should never have a negative count", () => {
    render(<Cart />);
    expect(screen.getByText(/Items in Cart: 0/i)).toBeInTheDocument();
  });
});
```

## **Advantages of Runtime-First TDD**
| Aspect            | Traditional TDD  | Runtime-First TDD 🚀 |
|------------------|----------------|----------------|
| **Test Writing** | Manual | Automated via annotations |
| **Starting Point** | Intentionally failing tests | Tests naturally match expected behavior |
| **Cognitive Load** | High (tests are separate from execution) | Low (tests embedded in runtime logic) |
| **Test Maintenance** | Breaks on refactor | Automatically updates |
| **Debugging** | Requires mapping failures to logic | Failures point directly to runtime behavior |

This model **makes TDD more intuitive, efficient, and scalable**.

## Conclusion: The Future of Runtime-First Development
The Runtime-First paradigm and Runtime-First TDD represent a fundamental shift in software development. By prioritizing execution over structure, this approach:

1. Reduces cognitive load, making code easier to understand.
2. Automatically ensures correctness, removing the need for redundant testing.
3. Keeps tests synchronized with code, eliminating common TDD pain points.

This methodology could redefine best practices across UI development, game programming, distributed systems, and AI-driven applications. With a transpiler that generates both implementation and tests, the barrier to adopting test-driven methodologies disappears—ushering in a new era of execution-first software development.
