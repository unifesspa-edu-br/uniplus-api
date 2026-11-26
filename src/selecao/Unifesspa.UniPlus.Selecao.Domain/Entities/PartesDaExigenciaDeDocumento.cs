namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed record CopiaDoTipoDeDocumento(Guid Id, string Codigo, string Nome, string Categoria, ProtecaoDoDocumento Protecao);

/// <summary>A fase, a etapa e o formulário em que o documento é coletado: toda exigência pertence a um formulário.</summary>
public sealed record MomentoDaExigencia(Guid FaseId, Guid? EtapaId, CodigoDoFormulario Formulario);

public sealed record AplicabilidadeDaExigencia(Aplicabilidade? Tipo, IReadOnlyList<CondicaoGatilho> Condicoes);

public sealed record IndeferimentoDoDocumento(bool Obrigatorio, string? ConsequenciaDoDescumprimento, IReadOnlyList<DocumentoExigidoBaseLegal> BasesLegais, bool AbrePrazoParaReenvio = false);

public sealed record DocumentoExigidoDeclarado(
    CopiaDoTipoDeDocumento Tipo, MomentoDaExigencia Momento, AplicabilidadeDaExigencia Aplicabilidade,
    IndeferimentoDoDocumento Indeferimento, RequisitosDoDocumento Requisitos, IReadOnlyList<string>? FatosComprovados);
