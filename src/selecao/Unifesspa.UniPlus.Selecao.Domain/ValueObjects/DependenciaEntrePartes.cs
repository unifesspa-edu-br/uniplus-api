namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Dependência da parte de destino em relação à parte de origem.</summary>
public sealed record DependenciaEntrePartes(MotivoDaDependencia Motivo, ParteDaConfiguracao Origem, ParteDaConfiguracao Destino);
