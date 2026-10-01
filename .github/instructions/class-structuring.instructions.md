---
description: "Structure of classes and structs"
applyTo: "**/*.cs"
---
# Structure of classes
This convention defines how classes (and structs) are structured.


## General
- Use `sealed` for classes that are not designed for inheritance.
- Validate input on public methods and throw `ArgumentException`. Use exception helpers when
  available (e.g. `ArgumentNullException.ThrowIfNull`).


## Regions
Organize the members of a class into `#region` blocks, in the following order. Omit regions if the
class has only members of a single kind.

1. `Nested definitions` — private nested helper types used only by the containing type
2. `Constants` — constant values (e.g. `const` and `readonly static` fields)
3. `Fields` — private instance and static fields
4. `Properties` — all properties ordered by visibility (public first)
5. `Constructors` — all constructors (instance and static) ordered by visibility (public first)
6. `Methods` — all methods ordered by visibility (public first)

Nested types follow the same region layout but do not use the region directives.

Nested regions are avoided. Instead, regions can be split to improve readability (e.g.
`Methods: Role A`, `Methods: Role B`).


## Documentation
- Every type, field, constructor, property, and method — including private ones — has an XML doc
  `<summary>` comment describing its purpose.
- Interface member implementations and overrides use `/// <inheritdoc/>` instead of repeating
  documentation.
