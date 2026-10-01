---
description: "Structure of unit tests"
applyTo: "tests/**/*.cs"
---
# Structure of unit tests
This convention defines how unit tests are structured.


## General
- Locate the test class in the same namespace as the class under test.
- Annotate the test class with `[TestFixture(TestOf = typeof(TargetType))]`, where `TargetType` is the
  class under test.
- Name the test class `<TargetType>Tests`.
- Group tests into `#region "Tests: <MethodName> method"` blocks, one region per public member.
  Use `#region Tests: Constructors` for constructor tests.
- Place a `#region Helpers` block at the end of the class containing shared helper/factory methods
  or constants.


## Naming
Use the following pattern: `MethodName_ExpectedBehaviorWhenCondition`
(e.g. `Enqueue_ThrowsObjectDisposedExceptionWhenDisposed`).


## Test Methods
- Structure each test body with `// Arrange`, `// Act`, and `// Assert` comments, in that order.
- Use `Assert.That(...)` constraint-based assertions instead of classic `Assert.AreEqual` style
  asserts.
- Use `Assert.EnterMultipleScope()` when asserting multiple independent conditions in a single test.
- Discard unused results with `_ = ...` when valuable.


## Asynchronous Test Methods
Guard async tests with `[Test, CancelAfter(Timeout)]` and accept a `CancellationToken` parameter:

  ```csharp
  [Test, CancelAfter(1_000)]
  public async Task CompleteNext_CompletesTaskWithFactoryResult(CancellationToken cancellationToken)
  {
      // ...
  }
  ```


## Exceptions
For methods expected to throw synchronously, wrap the call in a local `void ActFunction()` and
assert with:

  ```csharp
  Assert.That(ActFunction, Throws.InstanceOf<TException>()
      .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("paramName"));
  ```

For asynchronous methods expected to throw use:

  ```csharp
  await Assert.ThatAsync(ActFunction, Throws.InstanceOf<TException>());
  ```


## Coverage
For each public member under test, cover:
- Argument validation
- Cancellation before and during waiting
- Disposal fault propagation and post-dispose `ObjectDisposedException` on all public members.
