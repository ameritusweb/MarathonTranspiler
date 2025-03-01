# Marathon CLI

Command Line Interface for Marathon Transpiler, a Runtime-First code transpiler.

## Installation

### Option 1: Install as a .NET Tool

```bash
dotnet tool install --global Marathon.CLI
```

### Option 2: Build from Source

```bash
git clone https://github.com/ameritusweb/MarathonTranspiler.git
cd cli/Marathon.CLI
dotnet pack
dotnet tool install --global --add-source ./nupkg Marathon.CLI
```

## Usage

### Initialize a New Project

```bash
marathon init --target csharp
```

This creates a basic `mrtconfig.json` file and an example `.mrt` file in the `src` directory.

### Transpile Files

```bash
marathon transpile
```

Options:
- `-c, --config <PATH>`: Path to configuration file (default: mrtconfig.json)
- `-o, --output <DIR>`: Output directory for generated files
- `-v, --verbose`: Enable verbose output

### Watch for Changes

```bash
marathon watch
```

This will continuously watch for changes in `.mrt` files and transpile them automatically.

Options:
- Same as the `transpile` command

### List Available Inlineable Functions

```bash
marathon list
```

Options:
- `-t, --target <TARGET>`: Filter functions by target (e.g., csharp, react)
- `-v, --verbose`: Show detailed information about functions

## Configuration

The `mrtconfig.json` file controls the transpiler settings:

```json
{
  "transpilerOptions": {
    "target": "csharp",
    "csharp": {
      "testFramework": "xunit"
    }
  },
  "include": ["**/*.mrt"],
  "exclude": ["**/node_modules/**", "**/bin/**", "**/obj/**"],
  "rootDir": "./src"
}
```

## Example

1. Initialize a project:
   ```bash
   marathon init --target csharp
   ```

2. Modify the example file or create new `.mrt` files:
   ```csharp
   @varInit(className="Counter", type="int")
   this.count = 0;

   @run(className="Counter", functionName="increment")
   this.count++;
   Console.WriteLine($"Count: {this.count}");
   ```

3. Transpile:
   ```bash
   marathon transpile
   ```

4. Check the output `.cs` file.

## Documentation

For more information about Marathon Transpiler and the Runtime-First approach, see the [main documentation](https://github.com/ameritusweb/MarathonTranspiler).