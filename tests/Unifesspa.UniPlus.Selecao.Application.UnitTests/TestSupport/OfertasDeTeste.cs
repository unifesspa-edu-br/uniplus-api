namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Processos de duas ofertas de curso com conjuntos de modalidades diferentes: a ampla (<c>AC</c>)
/// vale para todos, e cada modalidade de ação afirmativa, para quem responde o fato dado com o valor
/// dado. Monta a configuração publicada diretamente, sem passar pela configuração do processo.
/// </summary>
internal static class OfertasDeTeste
{
    public const string ModalidadeDaAmpla = "AC";

    /// <summary>O fato e o valor que fazem valer as modalidades de ação afirmativa dos testes.</summary>
    public const string FatoDaAcaoAfirmativa = "SEXO";

    public const string ValorDaAcaoAfirmativa = "FEMININO";

    public static ConfiguracaoDistribuicaoVagas Distribuicao(Guid ofertaCursoId, params string[] codigos) =>
        DistribuicaoDoCurso(ofertaCursoId, "Medicina", codigos);

    public static ConfiguracaoDistribuicaoVagas DistribuicaoDoCurso(Guid ofertaCursoId, string cursoNome, params string[] codigos) =>
        ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoId,
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10 * codigos.Length, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)), Modalidades = [.. codigos.Select(Modalidade)] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoSnapshot.Criar($"OFERTA_{ofertaCursoId.ToString("N")[..12].ToUpperInvariant()}", cursoNome, "Bacharelado", "Campus Universitário de Marabá", ["MATUTINO"]).Value! }).Value!;

    /// <summary>A mesma configuração, com a distribuição de vagas dada no lugar da que ela tinha.</summary>
    public static ConfiguracaoPublicada ComDistribuicao(ConfiguracaoPublicada configuracao, params ConfiguracaoDistribuicaoVagas[] distribuicao) =>
        Reconstruir(configuracao, distribuicao, configuracao.Vagas.NumeroDeOpcoesDeCurso);

    /// <summary>A mesma configuração, declarando quantas opções de curso a inscrição admite.</summary>
    public static ConfiguracaoPublicada ComOpcoesDeCurso(ConfiguracaoPublicada configuracao, int numeroDeOpcoesDeCurso) =>
        Reconstruir(configuracao, [.. configuracao.Vagas.DistribuicaoVagas], numeroDeOpcoesDeCurso);

    private static ConfiguracaoPublicada Reconstruir(
        ConfiguracaoPublicada configuracao, ConfiguracaoDistribuicaoVagas[] distribuicao, int? numeroDeOpcoesDeCurso) =>
        ConfiguracaoPublicadaDeTeste.ComVagas(configuracao, new VagasDeclaradas(distribuicao, numeroDeOpcoesDeCurso,
            configuracao.Vagas.AplicaBonusRegional, configuracao.Vagas.BonusRegional, configuracao.Vagas.Cascata, configuracao.Vagas.AtendimentoEspecializado));

    private static ModalidadeSelecionada Modalidade(string codigo) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null,
            codigo == ModalidadeDaAmpla ? NaturezaLegalModalidade.Ampla : NaturezaLegalModalidade.AcaoAfirmativa,
            ComposicaoVagasModalidade.SuplementarAoTotal, null, RegraRemanejamentoModalidade.Nenhuma,
            null, null, null,
            codigo == ModalidadeDaAmpla
                ? PredicadoDnf.SemClausula
                : PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(
                    FatoDaAcaoAfirmativa, Operador.Igual, JsonSerializer.SerializeToElement(ValorDaAcaoAfirmativa)).Value!)]).Value!,
            null, "base legal", quantidadeDeclarada: 10).Value!;
}
