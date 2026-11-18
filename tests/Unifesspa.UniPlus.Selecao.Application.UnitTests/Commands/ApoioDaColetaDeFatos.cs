namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;


using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O que os testes do handler de coleta de fatos compartilham: os dublês, o vocabulário do catálogo e os processos de partida.</summary>
internal static class ApoioDaColetaDeFatos
{
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

    internal static Task<Result> HandleAsync(Mocks mocks, DefinirFatosColetadosCommand command) =>
        DefinirFatosColetadosCommandHandler.Handle(
            command, mocks.Repository, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

    /// <summary>Define os itens no formulário de habilitação do processo, com o catálogo do vocabulário de teste.</summary>
    internal static Task<Result> DefinirNaHabilitacaoAsync(ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput> itens) =>
        HandleAsync(
            NovosMocks(processo, processo.Id),
            new DefinirFatosColetadosCommand(processo.Id, FinalidadeFormulario.Habilitacao, itens));

    /// <summary>Define os itens, e os grupos quando há, no formulário de inscrição do processo.</summary>
    internal static Task<Result> DefinirNaInscricaoAsync(
        Mocks mocks, ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput>? grupos = null) =>
        HandleAsync(mocks, new DefinirFatosColetadosCommand(processo.Id, FinalidadeFormulario.Inscricao, itens, grupos));

    internal static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
        [.. DeclaradosDoCandidato(), .. OpcoesDeCurso(), .. DerivadosDoCandidato(), .. DaHabilitacao()];

    private static FatoCandidatoView[] DeclaradosDoCandidato() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:BAIXA_RENDA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "NOME_SOCIAL", "Nome social", "Descrição do fato.", "TEXTO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:NOME_SOCIAL", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Formato: "NOME_PESSOA", ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "DATA_NASCIMENTO", "Data de nascimento", "Descrição do fato.", "DATA", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:DATA_NASCIMENTO", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "ENDERECO_RESIDENCIAL", "Endereço residencial", "Descrição do fato.", "ENDERECO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] OpcoesDeCurso() =>
    [
        new(Guid.CreateVersion7(), "OPCAO_CURSO_1", "1ª opção de curso", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_CURSO_1", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "OPCAO_LISTA_ESPERA", "Opção da lista de espera", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_LISTA_ESPERA", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "OPCAO_CURSO_2", "2ª opção de curso", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["MEDICINA"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_CURSO_2", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] DerivadosDoCandidato() =>
    [
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            null, "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "RENDA_PER_CAPITA", "Renda per capita", "Descrição do fato.", "NUMERICO", "DERIVADO", "ESCALAR",
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_DE_RENDA", "Faixa de renda", "Descrição do fato.", "CATEGORICO", "DERIVADO", "ESCALAR",
            ["MENOR_DE_18", "DE_18_A_59", "60_OU_MAIS"], "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_DE_RENDA", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", "NUMERICO", "DERIVADO", "ESCALAR",
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] DaHabilitacao() =>
    [
        new(Guid.CreateVersion7(), "PARENTESCO", "Parentesco", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            ["PAI", "MAE", "FILHO"], "HABILITACAO", "CAMPO_FORMULARIO:PARENTESCO", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: "MEMBRO_GRUPO", ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "MENOR_SOB_GUARDA", "Menor sob guarda", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "HABILITACAO", "CAMPO_FORMULARIO:MENOR_SOB_GUARDA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: "MEMBRO_GRUPO", ProtecaoAprovada: true),
    ];

    internal static IEnumerable<string> CodigosDaConferencia(ProcessoSeletivo processo) =>
        ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors.Select(static e => e.Error.Code);

    internal static ProcessoSeletivo ProcessoEmRascunho()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Fatos", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Habilitacao);
        return processo;
    }

    internal static CondicaoPrecondicaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    internal static ProcessoSeletivo ProcessoComInscricao()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Inscricao);
        return processo;
    }

    internal static GrupoColetadoInput Composicao() => new(
        "COMPOSICAO_FAMILIAR", 1, "Composição familiar", FormularioDeTeste.Secao, 0, 10,
        [[Condicao("COR_RACA", "IGUAL", "PRETA")]], "NUNCA", null,
        [
            new FatoColetadoInput("PARENTESCO", 0, "Parentesco", "SELECAO_UNICA", "SEMPRE", null),
            new FatoColetadoInput("MENOR_SOB_GUARDA", 1, "Menor sob guarda", "BOOLEANO", "SEMPRE", [[Condicao("PARENTESCO", "IGUAL", "FILHO")]]),
        ]);

    internal static DefinirFatosColetadosCommand ComGrupo(ProcessoSeletivo processo, GrupoColetadoInput grupo) => new(
        processo.Id, FinalidadeFormulario.Habilitacao,
        [new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null, EtapaCodigo: FormularioDeTeste.Secao)],
        [grupo]);
}
