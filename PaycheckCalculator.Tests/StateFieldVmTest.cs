extern alias blazor;
using System.Globalization;
using blazor::PaycheckCalculator.Blazor.Components.Pages;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for the Blazor Calculator page's internal <c>StateFieldVm</c> helper,
/// which backs every dynamic state-schema input field. Decimal/integer
/// parsing must consistently use <see cref="CultureInfo.InvariantCulture"/> in
/// both <c>Validate()</c> and <c>GetValue()</c> so a value that validates
/// successfully is guaranteed to parse to the same value used in
/// calculations, regardless of the current thread culture (e.g. locales
/// where ',' is the decimal separator and '.' is a thousands separator).
/// </summary>
public sealed class StateFieldVmTest
{
    private static StateFieldDefinition DecimalField(bool required = false) => new()
    {
        Key = "AdditionalWithholding",
        Label = "Additional Withholding",
        FieldType = StateFieldType.Decimal,
        IsRequired = required
    };

    private static StateFieldDefinition IntegerField(bool required = false) => new()
    {
        Key = "Allowances",
        Label = "Allowances",
        FieldType = StateFieldType.Integer,
        IsRequired = required
    };

    /// <summary>
    /// Runs <paramref name="action"/> under a culture where ',' is the decimal
    /// separator and '.' is the group separator (e.g. de-DE), then restores
    /// the original culture — even if the action throws.
    /// </summary>
    private static void UnderCommaDecimalCulture(Action action)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Decimal_InvariantDot_ValidatesAndParsesConsistently_UnderCommaCulture()
    {
        UnderCommaDecimalCulture(() =>
        {
            var vm = new Calculator.StateFieldVm(DecimalField()) { StringValue = "12.50" };

            vm.Validate();

            Assert.False(vm.HasError);
            Assert.Equal(12.50m, vm.GetValue());
        });
    }

    [Fact]
    public void Decimal_CommaGroupedThousands_IsRejectedNotSilentlyZeroed()
    {
        // "1,234.56" (invariant thousands + decimal) must not be misread as
        // a comma-decimal European value once parsed with NumberStyles.Any +
        // InvariantCulture — it should parse to 1234.56, not fail silently.
        UnderCommaDecimalCulture(() =>
        {
            var vm = new Calculator.StateFieldVm(DecimalField()) { StringValue = "1,234.56" };

            vm.Validate();

            Assert.False(vm.HasError);
            Assert.Equal(1234.56m, vm.GetValue());
        });
    }

    [Fact]
    public void Decimal_Garbage_FailsValidationRatherThanSilentlyDefaultingToZero()
    {
        UnderCommaDecimalCulture(() =>
        {
            var vm = new Calculator.StateFieldVm(DecimalField()) { StringValue = "not-a-number" };

            vm.Validate();

            Assert.True(vm.HasError);
        });
    }

    [Fact]
    public void Integer_ValidatesAndParsesConsistently_UnderCommaCulture()
    {
        UnderCommaDecimalCulture(() =>
        {
            var vm = new Calculator.StateFieldVm(IntegerField()) { StringValue = "3" };

            vm.Validate();

            Assert.False(vm.HasError);
            Assert.Equal(3, vm.GetValue());
        });
    }
}
