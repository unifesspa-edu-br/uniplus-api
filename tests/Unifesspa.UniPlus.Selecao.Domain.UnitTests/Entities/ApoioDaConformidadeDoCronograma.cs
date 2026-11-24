namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem comum dos testes de conformidade do cronograma: a regra, a distribuição de vagas, os dados
/// do edital e a publicação, para que o gate de pendências e o do tipo de ato único partilhem o mesmo cenário.
/// </summary>
internal static class ApoioDaConformidadeDoCronograma
{
    internal static readonly string HashFixo = new('a', 64);

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static ConfiguracaoDistribuicaoVagas Distribuicao(int vagasOfertadasBase) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = vagasOfertadasBase,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), 'a'),
                Modalidades = [
                ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "Res. Unifesspa 532/2021" }, vagasOfertadasBase).Value!,
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    internal static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste" });

    /// <summary>
    /// Publicar sem declarar cobrança de taxa é recusado (CA-01) — declarada em
    /// todo teste deste arquivo para que a pendência de CRONOGRAMA isolada por cada um seja a
    /// única a aflorar, não mascarada pela pendência estrutural genérica.
    /// </summary>
    internal static void DeclararNaoCobra(ProcessoSeletivo processo)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }
}
