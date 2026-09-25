using Content.Shared._Misfits.Special;
using NUnit.Framework;

namespace Content.Tests.Shared.Misfits.Special;

[TestFixture]
public sealed class PerceptionScopeDelayTest
{
    [TestCase(-1, 3f)]
    [TestCase(1, 3f)]
    [TestCase(2, 2.5f)]
    [TestCase(3, 2f)]
    [TestCase(4, 1.5f)]
    [TestCase(5, 1f)]
    [TestCase(6, 0.82f)]
    [TestCase(7, 0.64f)]
    [TestCase(8, 0.46f)]
    [TestCase(9, 0.28f)]
    [TestCase(10, 0.1f)]
    [TestCase(15, 0.1f)]
    public void ScopeDelayRespectsPerceptionBalanceAndBounds(int perception, float expected)
    {
        Assert.That(SharedSpecialSystem.GetPerceptionScopeDelayMultiplier(perception, 3f, 0.1f),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void HigherPerceptionAlwaysReducesDelay()
    {
        for (var perception = 2; perception <= 10; perception++)
        {
            Assert.That(SharedSpecialSystem.GetPerceptionScopeDelayMultiplier(perception, 3f, 0.1f),
                Is.LessThan(SharedSpecialSystem.GetPerceptionScopeDelayMultiplier(perception - 1, 3f, 0.1f)));
        }
    }
}
