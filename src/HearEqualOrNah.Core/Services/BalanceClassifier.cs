namespace HearEqualOrNah.Core.Services;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Constants;

public sealed class BalanceClassifier : IBalanceClassifier
{
    public string Classify(double differenceDb)
    {
        return BalanceClassification.Classify(differenceDb);
    }

    public string GetDescription(double differenceDb)
    {
        return BalanceClassification.GetDescription(differenceDb);
    }

    public IReadOnlyList<string> GetPossibleCauses()
    {
        return PossibleCauses.ImbalanceCauses;
    }
}