# Contributing to JAG Probate

Thank you for your interest in contributing to the JAG Probate project!

## Development Workflow

1. **Fork the repository** (if external contributor)
2. **Create a feature branch** from `main`
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes** following our coding standards
4. **Test your changes** thoroughly
5. **Commit your changes** with clear commit messages
6. **Push to your branch**
7. **Create a Pull Request**

## Coding Standards

### C# / .NET

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose
- Write unit tests for business logic

### Entity Framework

- Use code-first migrations
- Name migrations descriptively
- Never delete or modify existing migrations in production branches

### Git Commits

- Use clear, descriptive commit messages
- Start with a verb (Add, Fix, Update, Remove, etc.)
- Reference issue numbers when applicable

Example:
```
Add case filtering by status

- Implement status filter in Cases controller
- Add unit tests for filtering logic
- Update API documentation

Fixes #123
```

## Pull Request Process

1. Ensure your code builds without errors
2. Update documentation if needed
3. Add/update tests as appropriate
4. Ensure all tests pass
5. Request review from maintainers
6. Address review feedback promptly

## Testing

Run tests locally before submitting PR:

```bash
cd api
dotnet test
```

## Questions?

Open an issue or contact the project maintainers.
