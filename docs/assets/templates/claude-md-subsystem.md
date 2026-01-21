# [Subsystem Name] - Claude Context

> This is a template for subsystem-level CLAUDE.md files. Adapt sections as needed.

## Purpose
[1-2 sentences: What does this subsystem do? What's its role in the solution?]

## Dependencies

**This subsystem depends on:**
- [Project/Service A] - [Why/how it's used]
- [Project/Service B] - [Why/how it's used]

**These depend on this subsystem:**
- [Project/Service C] - [What they consume]

## Critical Rules

- [Subsystem-specific rule 1]
- [Subsystem-specific rule 2]
- [Subsystem-specific rule 3]

## Code Structure

```
src/[SubsystemName]/
├── Services/           # [What goes here]
├── Models/             # [What goes here]
├── Abstractions/       # [What goes here]
└── [other folders]
```

## Coding Patterns

### Pattern 1: [Common Operation]
**✅ Good:**
```csharp
// Show the correct way
```

**❌ Bad:**
```csharp
// Show what to avoid
```

### Pattern 2: [Another Common Operation]
[Repeat as needed]

## Current Status

### ✅ Completed
- [Feature 1] - [Brief note]
- [Feature 2] - [Brief note]

### 🔄 In Progress
- [Feature 3] - [Current state, blockers if any]
- [Feature 4] - [Current state, blockers if any]

### 📋 Planned
- [Feature 5] - [When/why]
- [Feature 6] - [When/why]

## Roadmap

| ID | Status | Title | Exit Criteria | Dependencies |
|----|--------|-------|---------------|--------------|
| X1 | ✅ Done | [Task] | [What defines done] | [What needs to be ready] |
| X2 | 🔄 Active | [Task] | [What defines done] | X1 |
| X3 | 📋 Pending | [Task] | [What defines done] | X2 |

**OR use a simpler bullet format:**

- ✅ **Milestone 1** - [Brief description, link to design doc if exists]
- 🔄 **Milestone 2** - [What's done, what's left, blockers]
- 📋 **Milestone 3** - [Waiting on X, planned for Y]

## Common Tasks

### Task: [Add/Modify Feature X]
```bash
# Step-by-step commands or instructions
```

**Files to modify:**
- `path/to/file1.cs` - [What to change]
- `path/to/file2.cs` - [What to change]

**Tests to update:**
- `path/to/test.cs` - [What to verify]

### Task: [Debug Problem Y]
1. Check [log/metric/location]
2. Look for [symptom]
3. Common causes: [list]
4. Fix by: [approach]

### Task: [Run/Test Subsystem]
```bash
# Commands to run subsystem in isolation
dotnet test --filter "FullyQualifiedName~[SubsystemName]"
```

## Configuration

### Required Settings
```json
{
  "Setting1": "value",  // [What it controls]
  "Setting2": 123       // [What it controls]
}
```

### Environment Variables
- `VAR_NAME` - [Purpose, default value]

## Integration Points

### Consuming Service A
```csharp
// Example of how to call into dependency
var result = await _serviceA.DoSomethingAsync(param);
```

### Providing Data to Service B
```csharp
// Example of interface/contract this subsystem exposes
public interface IMyService
{
    Task<Result> DoWorkAsync(Input input);
}
```

## Testing Strategy

### Unit Tests
- Location: `tests/[SubsystemName].Tests/`
- Run: `dotnet test --filter "FullyQualifiedName~[SubsystemName].Tests"`
- Coverage expectations: [percentage or key areas]

### Integration Tests
- Location: `tests/[SubsystemName].Integration.Tests/`
- Dependencies: [Docker, Testcontainers, etc.]
- Run: `dotnet test --filter "Category=Integration"`

### Test Data
- Fixtures: `tests/[SubsystemName].Tests/Fixtures/`
- Seed data: [Where/how test data is created]

## Performance Considerations

- [Consideration 1] - [Why it matters, how to handle]
- [Consideration 2] - [Why it matters, how to handle]

## Known Issues & Workarounds

### Issue: [Problem Description]
**Symptom:** [What you'll see]
**Cause:** [Why it happens]
**Workaround:** [How to handle it]
**Tracking:** [Link to issue/TODO]

## Reference Documentation

**Design docs:**
- `docs/[subsystem-design].md` - [What it covers]

**Related subsystems:**
- `src/[OtherSubsystem]/CLAUDE.md` - [How it relates]

**External dependencies:**
- [Library name] - [Documentation link, version used]

## Debugging Tips

- **Problem:** [Common symptom]
  **Check:** [Where to look]
  **Fix:** [How to resolve]

- **Problem:** [Another common issue]
  **Check:** [Where to look]
  **Fix:** [How to resolve]

## Migration Notes

[If this subsystem is being rewritten/migrated]

**Legacy behavior:**
- [What the old system did]

**New behavior:**
- [What this system does differently]

**Compatibility:**
- [Breaking changes, migration paths]

## Maintenance

**Update this file when:**
- Adding new major features
- Changing integration contracts
- Discovering new patterns/anti-patterns
- Milestones shift (roadmap updates)

**Keep it focused:**
- This file is for working context, not tutorials
- Move detailed design to `docs/`
- Keep code examples minimal but illustrative
