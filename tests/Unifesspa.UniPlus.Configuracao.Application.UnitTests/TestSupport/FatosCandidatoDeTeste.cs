namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Fatos do catálogo válidos, cadastrados pelo administrador, para os cenários de leitura.</summary>
internal static class FatosCandidatoDeTeste
{
    public const string Codigo = "FATO_DE_TESTE";

    public static FatoCandidato Declarado(
        DominioFato dominio,
        CardinalidadeFato cardinalidade = CardinalidadeFato.Escalar,
        FonteValoresFato? fonteValores = null,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            Codigo, "Fato de teste", "Descrição do fato.", dominio, cardinalidade, fonteValores,
            dominio == DominioFato.Texto ? FormatoTexto.Livre : null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal, componenteExibicao).Value!;
}
