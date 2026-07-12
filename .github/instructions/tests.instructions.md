---
applyTo: "PaycheckCalculator.Tests/**/*.cs"
---

# Test instructions

- Use xUnit with readable scenario-based test names similar to the existing suite.
- Prefer explicit numeric expectations taken from the applicable rule/table/scenario rather than recomputing expected values with production helpers.
- Cover tax edge cases: bracket boundaries, exemptions, allowances, extra withholding, pre-tax deduction effects, rounding behavior, and state-specific exceptions.
- When changing a calculator, add or update regression tests in the calculator's corresponding test file.
- Tests should validate business behavior, not internal implementation details, unless the architecture itself is the thing being protected.
- Keep comments in tests useful and payroll-specific; they are part of the repository's living documentation.
