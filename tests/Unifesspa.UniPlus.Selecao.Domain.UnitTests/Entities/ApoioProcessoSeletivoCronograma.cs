namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioProcessoSeletivoCronograma
{
    internal static ProcessoSeletivo NovoProcesso(OrigemCandidatos origem = OrigemCandidatos.ImportacaoExterna) =>
        ProcessoSeletivo.Criar("PS Cronograma", TiposDeProcessoDeTeste.SiSU, origem, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static (FaseCronograma Inscricao, FaseCronograma Homologacao) InscricaoEHomologacaoComPeriodosSobrepostos()
    {
        FaseCronograma inscricao = new ModeloDeFase("INSCRICAO", 1)
        {
            ColetaInscricao = true,
            Inicio = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
        }.Criar();
        FaseCronograma homologacao = new ModeloDeFase("HOMOLOGACAO_INSCRICOES", 2)
        {
            Inicio = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
        }.Criar();
        return (inscricao, homologacao);
    }

    internal static IReadOnlyList<ProdutoDaFase> Preliminar(string atoCodigo = "RESULTADO_CLASSIFICACAO") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelDaPublicacaoPrevista.Preliminar)];

    internal static IReadOnlyList<ProdutoDaFase> Definitiva(string atoCodigo = "RESULTADO_CLASSIFICACAO") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelDaPublicacaoPrevista.Definitivo)];
}
