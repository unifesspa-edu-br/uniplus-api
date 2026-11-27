namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
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

    internal static FatoColetadoInput[] ItensAcimaDoTeto() =>
        [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1)
            .Select(static i => EntradaDoFatoColetadoDeTeste.Coletando($"FATO_{i}", i).ComRotulo("Campo").Construir())];

    internal static async Task ConferirQueNadaFoiLidoNemGravadoAsync(Mocks mocks)
    {
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Define os itens no formulário de habilitação do processo, com o catálogo do vocabulário de teste.</summary>
    internal static Task<Result> DefinirNaHabilitacaoAsync(ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput> itens) =>
        HandleAsync(
            NovosMocks(processo, processo.Id),
            new DefinirFatosColetadosCommand(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), itens));

    /// <summary>Define os itens, e os grupos quando há, no formulário de inscrição do processo.</summary>
    internal static Task<Result> DefinirNaInscricaoAsync(
        Mocks mocks, ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput>? grupos = null) =>
        HandleAsync(mocks, new DefinirFatosColetadosCommand(processo.Id, PapelDoFormularioDeTeste.Inscricao.Codigo(), itens, grupos));

    internal static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
        [.. DeclaradosDoCandidato(), .. OpcoesDeCurso(), .. DerivadosDoCandidato(), .. DaHabilitacao()];

    private static FatoCandidatoView[] DeclaradosDoCandidato() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "CAMPO_FORMULARIO:BAIXA_RENDA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "NOME_SOCIAL", "Nome social", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Texto), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "CAMPO_FORMULARIO:NOME_SOCIAL", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Formato: CodigoComRotulo<FormatoTexto>.De(CatalogoDe<FormatoTexto>.Obter("NOME_PESSOA")), ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "DATA_NASCIMENTO", "Data de nascimento", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Data), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "CAMPO_FORMULARIO:DATA_NASCIMENTO", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "ENDERECO_RESIDENCIAL", "Endereço residencial", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Endereco), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] OpcoesDeCurso() =>
    [
        new(Guid.CreateVersion7(), "OPCAO_CURSO_1", "1ª opção de curso", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_CURSO_1", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "OPCAO_LISTA_ESPERA", "Opção da lista de espera", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["MEDICINA", "ENFERMAGEM"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_LISTA_ESPERA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "OPCAO_CURSO_2", "2ª opção de curso", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["MEDICINA"], "INSCRICAO", "CAMPO_FORMULARIO:OPCAO_CURSO_2", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] DerivadosDoCandidato() =>
    [
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores),
            null, "INSCRICAO", "REGRA_DE_CALCULO:MODALIDADE", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Modalidade), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "RENDA_PER_CAPITA", "Renda per capita", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Numerico), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_DE_RENDA", "Faixa de renda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["MENOR_DE_18", "DE_18_A_59", "60_OU_MAIS"], "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_DE_RENDA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Numerico), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static FatoCandidatoView[] DaHabilitacao() =>
    [
        new(Guid.CreateVersion7(), "PARENTESCO", "Parentesco", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["PAI", "MAE", "FILHO"], "HABILITACAO", "CAMPO_FORMULARIO:PARENTESCO", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: CodigoComRotulo<EscopoFato>.De(CatalogoDe<EscopoFato>.Obter("MEMBRO_GRUPO")), ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "MENOR_SOB_GUARDA", "Menor sob guarda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "HABILITACAO", "CAMPO_FORMULARIO:MENOR_SOB_GUARDA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: CodigoComRotulo<EscopoFato>.De(CatalogoDe<EscopoFato>.Obter("MEMBRO_GRUPO")), ProtecaoAprovada: true),
    ];

    internal static IEnumerable<string> CodigosDaConferencia(ProcessoSeletivo processo) =>
        ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors.Select(static e => e.Error.Code);

    internal static ProcessoSeletivo ProcessoEmRascunho()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Fatos", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);
        return processo;
    }

    internal static CondicaoPrecondicaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    /// <summary>O processo com o formulário de inscrição ligado à fase do cronograma que coleta inscrição, como a inscrição é definida.</summary>
    internal static ProcessoSeletivo ProcessoComInscricao()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        processo.DefinirCronogramaFases([FaseQueColetaInscricao()], []).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        return processo;
    }

    private static FaseCronograma FaseQueColetaInscricao() => FaseCronograma.Criar(ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    internal static GrupoColetadoInput Composicao() =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao(FormularioDeTeste.Secao)
            .ComOcorrencias(0, 10).ExibidoQuando([[Condicao("COR_RACA", "IGUAL", "PRETA")]])
            .ComCampos(
                EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").ComRotulo("Parentesco").DoTipo("ESCOLHA_UNICA").Construir(),
                EntradaDoFatoColetadoDeTeste.Coletando("MENOR_SOB_GUARDA", 1).ComRotulo("Menor sob guarda")
                    .ExibidoQuando([[Condicao("PARENTESCO", "IGUAL", "FILHO")]]).Construir())
            .Construir();

    internal static DefinirFatosColetadosCommand ComGrupo(ProcessoSeletivo processo, GrupoColetadoInput grupo) => new(
        processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(),
        [EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").NaSecao(FormularioDeTeste.Secao).Construir()],
        [grupo]);
}
