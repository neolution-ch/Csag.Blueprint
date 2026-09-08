---
"@neolution-ch/csag-blueprint-source-generators": patch
---

Harden the translation-key generator's edge-case handling and make its output deterministic:

- A `TranslationDefaults` class in the global namespace now generates compilable sources: the
  generated files omit the namespace declaration instead of emitting the invalid
  `namespace <global namespace>;`.
- A non-partial `TranslationDefaults` class no longer breaks the consuming build with an
  unexplained `CS0260`; the generator reports warning `CSAGGEN001` and skips generation for
  that class.
- Multiple `TranslationDefaults` classes in one compilation are still merged first-wins, but the
  merge is now surfaced as warning `CSAGGEN002` naming every participating class.
- Generated keys, constants, and properties are sorted with ordinal string comparison instead of
  the culture-sensitive default, so the output no longer depends on the build machine's culture.
