namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>Dublês, fatos aprovados e montagem de comandos comuns aos testes do handler do modelo de formulário.</summary>
internal sealed class CenarioDoHandlerDoModelo
{
    public const string Finalidade = "Verificação dos requisitos do processo seletivo.";
    public const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    public IModeloFormularioRepository Repository { get; } = Substitute.For<IModeloFormularioRepository>();
    public IFatoCandidatoRepository Fatos { get; } = Substitute.For<IFatoCandidatoRepository>();
    public ITermoConsentimentoReader Termos { get; } = Substitute.For<ITermoConsentimentoReader>();
    public ITipoProcessoReader Tipos { get; } = Substitute.For<ITipoProcessoReader>();
    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public FatoCandidato Certificado { get; } = Declarado("CERTIFICADO");
    public FatoCandidato DataNascimento { get; } = Declarado("DATA_NASCIMENTO", TipoDoDado.Data);
    public FatoCandidato FaixaEtaria { get; } = CriacaoDoFato.Criar(
        new DescritivoDoFato("FAIXA_ETARIA", "Faixa etária", "Descrição do fato."),
        OrigemFato.Calculado,
        "ATRIBUTO_CANDIDATO:FAIXA_ETARIA",
        new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese),
        sistema: true).Value!;
    public FatoCandidato MaiorIdade { get; } = Declarado("MAIOR_IDADE", escopo: EscopoFato.MembroGrupo);
    public FatoCandidato SemRenda { get; } = Declarado("SEM_RENDA", escopo: EscopoFato.MembroGrupo);
    public FatoCandidato Cpf { get; } = Declarado("CPF_RESPONSAVEL", TipoDoDado.Texto, FormatoTexto.Cpf);
    public FatoCandidato Idade { get; } = CriacaoDoFato.Criar(
        new DescritivoDoFato("IDADE", "Idade", "Descrição do fato."),
        OrigemFato.Calculado,
        "ATRIBUTO_CANDIDATO:IDADE",
        new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese),
        sistema: true).Value!;

    public CenarioDoHandlerDoModelo()
    {
        foreach (FatoCandidato fato in new[] { Certificado, Cpf, Idade, MaiorIdade, SemRenda, DataNascimento, FaixaEtaria })
        {
            Aprovar(fato);
        }

        Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [Certificado, Cpf, Idade, MaiorIdade, SemRenda, DataNascimento, FaixaEtaria]);
        Termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        Tipos.ObterAtivoPorCodigoAsync("REGULAR_UNIFICADO", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), "REGULAR_UNIFICADO", "REGULAR_UNIFICADO", null));
    }

    public static void Aprovar(FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        fato.SolicitarAprovacaoDaProtecao("isabella.figueiredo", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
        fato.AprovarProtecao("caroline.lima", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
    }

    public Task<Result<Guid>> CriarAsync(ConteudoDoModeloInput conteudo, string? tipo = null) =>
        CriarAsync("HABILITACAO_MEDICINA", new DescritivoDoModeloInput("Habilitação Medicina", null, tipo), conteudo);

    public Task<Result<Guid>> CriarAsync(string codigo, DescritivoDoModeloInput descritivo, ConteudoDoModeloInput conteudo) =>
        CriarAsync(new CriarModeloFormularioCommand(codigo, EntradaDeOutraFaseObrigatoria, descritivo, conteudo));

    public Task<Result<Guid>> CriarAsync(CriarModeloFormularioCommand comando) =>
        CriarModeloFormularioCommandHandler.Handle(comando, Repository, Fatos, Termos, Tipos, UnitOfWork, CancellationToken.None);

    public Task<Result> AtualizarAsync(ModeloFormulario existente, ConteudoDoModeloInput conteudo, string? tipo) =>
        AtualizarAsync(existente, new DescritivoDoModeloInput("Habilitação Medicina", null, tipo), conteudo);

    public Task<Result> AtualizarAsync(ModeloFormulario existente, DescritivoDoModeloInput descritivo, ConteudoDoModeloInput conteudo)
    {
        ArgumentNullException.ThrowIfNull(existente);
        Repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return AtualizarModeloFormularioCommandHandler.Handle(
            new AtualizarModeloFormularioCommand(existente.Id, descritivo, conteudo),
            Repository, Fatos, Termos, Tipos, UnitOfWork, CancellationToken.None);
    }

    /// <summary>O modelo gravado com o certificado como item, antes de qualquer mudança no catálogo.</summary>
    public static ModeloFormulario Existente(string? tipo)
    {
        ItemDoModelo item = new(
            "CERTIFICADO", 0, "DADOS", new ApresentacaoDoCampo("Certificado", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));
        return ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", new DescritivoDoModelo("Habilitação Medicina", null, tipo), DeOutraFaseObrigatoria,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }

    public static readonly PaginaDoModelo Secao = new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null);
    public static readonly PaginaDoModelo Revisao = new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    public static ConteudoDoModeloInput Conteudo(params FatoColetadoInput[] itens) => new(
        "Habilitação",
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir(),
        ],
        itens,
        [],
        []);

    public static ConteudoDoModeloInput ComComposicao(params FatoColetadoInput[] campos) =>
        Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos = [EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao("DADOS")
                .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(campos).Construir()],
        };

    /// <summary>O item que a leitura recusa: a obrigatoriedade não é uma das conhecidas.</summary>
    public static FatoColetadoInput ItemNaoLido(string fato, int ordem) => Montar(fato, ordem).ComObrigatoriedade("INEXISTENTE").Construir();

    /// <summary>O item que a leitura aceita e o domínio recusa: o rótulo está vazio.</summary>
    public static FatoColetadoInput ItemSemRotulo(string fato, int ordem, string tipo = "SIM_OU_NAO") =>
        Montar(fato, ordem, tipo).ComRotulo(string.Empty).Construir();

    /// <summary>O conteúdo com a segunda página de código dado, no lugar da revisão.</summary>
    public static ConteudoDoModeloInput ComSegundaPagina(ConteudoDoModeloInput conteudo, string codigo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return conteudo with { Paginas = [conteudo.Paginas![0], EntradaDaPaginaDoFormularioDeTeste.Secao(codigo, 1).ComTitulo("Outra").Construir()] };
    }

    public static FatoColetadoInput Campo(string fato, int ordem, string tipo = "SIM_OU_NAO") => Montar(fato, ordem, tipo).NaSecao(null).Construir();

    public static FatoColetadoInput Item(string fato, int ordem, string tipo = "SIM_OU_NAO") => Montar(fato, ordem, tipo).Construir();

    /// <summary>O item na seção <c>DADOS</c>, ainda sem fechar, para o teste que lhe dá regras.</summary>
    public static EntradaDoFatoColetadoDeTeste Montar(string fato, int ordem, string tipo = "SIM_OU_NAO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).DoTipo(tipo).NaSecao("DADOS");

    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    public static FatoCandidato Declarado(
        string codigo, TipoDoDado dominio = TipoDoDado.SimOuNao, FormatoTexto? formato = null, EscopoFato escopo = EscopoFato.Candidato) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, null, formato, "INSCRICAO", escopo, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
}
