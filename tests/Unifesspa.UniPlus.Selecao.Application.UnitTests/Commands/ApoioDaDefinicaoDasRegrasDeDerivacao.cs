namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Apoio dos testes do <see cref="DefinirRegrasDerivacaoCommandHandler"/>: o vocabulário de fatos, os processos em
/// rascunho e a montagem das entradas e do handler.
/// </summary>
internal static class ApoioDaDefinicaoDasRegrasDeDerivacao
{
    internal const string Derivado = "REGIME_DE_VAGA";

    internal const string HashFixo = "ab01234567ab01234567ab01234567ab01234567ab01234567ab01234567ab01";

    internal sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        IUnitOfWork UnitOfWork);

    internal static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        Mocks mocks = new(repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<IUnitOfWork>());
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(VocabularioSeed()));
        return mocks;
    }

    internal static Task<Result> HandleAsync(Mocks mocks, DefinirRegrasDerivacaoCommand command) =>
        DefinirRegrasDerivacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

    internal static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:BAIXA_RENDA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), OrigemEscolar.FatoEgresso, "Egresso de escola pública", "Descrição do fato.", "BOOLEANO", "DERIVADO", "ESCALAR",
            null, "INSCRICAO", $"REGRA_DERIVACAO:{OrigemEscolar.FatoEgresso}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), Derivado, "Regime de vaga", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            ["AC", "AC_PCD"], "INSCRICAO", $"REGRA_DERIVACAO:{Derivado}",
            [new FatoValorDominioViewItem("AC", "Ampla", 0, true), new FatoValorDominioViewItem("AC_PCD", "Reserva", 1, true)],
            "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    internal static ProcessoSeletivo ProcessoBase() =>
        ProcessoSeletivo.Criar("PS Regras", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>Processo em rascunho que oferta a modalidade AC e coleta COR_RACA — o mínimo para uma derivação válida.</summary>
    internal static ProcessoSeletivo ProcessoComModalidadeAcEColeta()
    {
        ProcessoSeletivo processo = ProcessoBase();

        processo.DefinirItens([FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!])
            .IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo);
        ModalidadeSelecionada ac = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [ac] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        return processo;
    }

    internal static ModalidadeSelecionada ModalidadeDe(string codigo, NaturezaLegalModalidade natureza) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), null, "base legal", quantidadeDeclarada: 20).Value!;

    internal static CondicaoPrecondicaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    internal static ConfiguracaoDerivacaoInput ConfigDerivado(params RegraDerivacaoInput[] regras) =>
        new(Derivado, regras);
}
