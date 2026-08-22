namespace TutorSphere.Application.Common;

/// <summary>
/// Répartition d’un paiement parent : d’abord les frais Stripe/PayPal sur le brut,
/// puis la commission plateforme sur le net, le reste au groupe ou à l’enseignant.
/// </summary>
public sealed record ParentPaymentSplit(
    decimal Gross,
    decimal ProcessorFee,
    decimal Net,
    decimal PlatformFee,
    decimal Remainder,
    decimal TutorAmount,
    decimal GroupAmount,
    Guid? ExpertGroupId,
    decimal CommissionPercent,
    decimal ProcessorFeePercent,
    decimal ProcessorFeeFixed);

public static class ParentPaymentSplitCalculator
{
    public const decimal DefaultCommissionPercent = 30m;
    public const decimal DefaultCardFeePercent = 2.9m;
    public const decimal DefaultCardFeeFixed = 0.30m;
    public const decimal DefaultPayPalFeePercent = 2.9m;
    public const decimal DefaultPayPalFeeFixed = 0.30m;

    public static decimal ClampCommission(decimal percent) =>
        Math.Clamp(percent, 0m, 100m);

    public static decimal ClampFeePercent(decimal percent) =>
        Math.Clamp(percent, 0m, 100m);

    public static decimal ClampFeeFixed(decimal amount) =>
        Math.Max(0m, decimal.Round(amount, 2, MidpointRounding.AwayFromZero));

    public static ParentPaymentSplit Compute(
        decimal gross,
        decimal processorFeePercent,
        decimal processorFeeFixed,
        decimal commissionPercent,
        Guid? expertGroupId)
    {
        gross = Math.Max(0m, decimal.Round(gross, 2, MidpointRounding.AwayFromZero));
        processorFeePercent = ClampFeePercent(processorFeePercent);
        processorFeeFixed = ClampFeeFixed(processorFeeFixed);
        commissionPercent = ClampCommission(commissionPercent);

        var processorFee = decimal.Round(
            gross * processorFeePercent / 100m + processorFeeFixed,
            2,
            MidpointRounding.AwayFromZero);
        if (processorFee > gross)
            processorFee = gross;

        var net = gross - processorFee;
        var platformFee = decimal.Round(net * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
        if (platformFee > net)
            platformFee = net;

        var remainder = net - platformFee;
        var toGroup = expertGroupId is Guid id && id != Guid.Empty;

        return new ParentPaymentSplit(
            Gross: gross,
            ProcessorFee: processorFee,
            Net: net,
            PlatformFee: platformFee,
            Remainder: remainder,
            TutorAmount: toGroup ? 0m : remainder,
            GroupAmount: toGroup ? remainder : 0m,
            ExpertGroupId: toGroup ? expertGroupId : null,
            CommissionPercent: commissionPercent,
            ProcessorFeePercent: processorFeePercent,
            ProcessorFeeFixed: processorFeeFixed);
    }
}
