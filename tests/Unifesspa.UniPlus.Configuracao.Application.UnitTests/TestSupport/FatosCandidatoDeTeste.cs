namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Fatos do catálogo válidos, cadastrados pelo administrador, para os cenários de leitura.</summary>
internal static class FatosCandidatoDeTeste
{
    public const string Codigo = "FATO_DE_TESTE";

    public static FatoCandidato Declarado(
        TipoDoDado dominio,
        QuantidadeDeValores cardinalidade = QuantidadeDeValores.ValorUnico,
        FonteValoresFato? fonteValores = null,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(Codigo, "Fato de teste", "Descrição do fato.", componenteExibicao),
            new DefinicaoDoFato(dominio, cardinalidade, fonteValores, dominio == TipoDoDado.Texto ? FormatoTexto.Livre : null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
}
