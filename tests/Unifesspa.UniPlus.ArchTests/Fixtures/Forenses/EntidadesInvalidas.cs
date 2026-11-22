namespace FixturesForenses;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public class EntidadeNaoSelada : IForensicEntity
{
    public Guid Id { get; init; }
}

public sealed class EntidadeComHeranca : EntityBase, IForensicEntity;
