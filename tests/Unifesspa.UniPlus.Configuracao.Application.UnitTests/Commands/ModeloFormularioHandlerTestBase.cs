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

/// <summary>Dublês, fatos aprovados e montagem de comandos comuns aos testes do handler do modelo de formulário.</summary>
public abstract class ModeloFormularioHandlerTestBase
{
    protected const string Finalidade = "Verificação dos requisitos do processo seletivo.";
    protected const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    protected IModeloFormularioRepository Repository { get; } = Substitute.For<IModeloFormularioRepository>();
    protected IFatoCandidatoRepository Fatos { get; } = Substitute.For<IFatoCandidatoRepository>();
    protected ITermoConsentimentoReader Termos { get; } = Substitute.For<ITermoConsentimentoReader>();
    protected ITipoProcessoReader Tipos { get; } = Substitute.For<ITipoProcessoReader>();
    protected IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    protected FatoCandidato Certificado { get; } = Declarado("CERTIFICADO");
    protected FatoCandidato DataNascimento { get; } = Declarado("DATA_NASCIMENTO", TipoDoDado.Data);
    protected FatoCandidato FaixaEtaria { get; } = CriacaoDoFato.Criar(
        new DescritivoDoFato("FAIXA_ETARIA", "Faixa etária", "Descrição do fato."),
        OrigemFato.Calculado,
        "ATRIBUTO_CANDIDATO:FAIXA_ETARIA",
        new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese),
        sistema: true).Value!;
    protected FatoCandidato MaiorIdade { get; } = Declarado("MAIOR_IDADE", escopo: EscopoFato.MembroGrupo);
    protected FatoCandidato SemRenda { get; } = Declarado("SEM_RENDA", escopo: EscopoFato.MembroGrupo);
    protected FatoCandidato Cpf { get; } = Declarado("CPF_RESPONSAVEL", TipoDoDado.Texto, FormatoTexto.Cpf);
    protected FatoCandidato Idade { get; } = CriacaoDoFato.Criar(
        new DescritivoDoFato("IDADE", "Idade", "Descrição do fato."),
        OrigemFato.Calculado,
        "ATRIBUTO_CANDIDATO:IDADE",
        new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese),
        sistema: true).Value!;

    protected ModeloFormularioHandlerTestBase()
    {
        foreach (FatoCandidato fato in new[] { Certificado, Cpf, Idade, MaiorIdade, SemRenda, DataNascimento, FaixaEtaria })
        {
            Aprovar(fato);
        }

        Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [Certificado, Cpf, Idade, MaiorIdade, SemRenda, DataNascimento, FaixaEtaria]);
        Termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        Tipos.ObterAtivoPorCodigoAsync("PSR", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), "PSR", "PSR", null));
    }

    protected static void Aprovar(FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        fato.SolicitarAprovacaoDaProtecao("isabella.figueiredo", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
        fato.AprovarProtecao("caroline.lima", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
    }

    protected Task<Result<Guid>> CriarAsync(ConteudoDoModeloInput conteudo, string? tipo = null) =>
        CriarModeloFormularioCommandHandler.Handle(
            new CriarModeloFormularioCommand("HABILITACAO_MEDICINA", "HABILITACAO", new DescritivoDoModeloInput("Habilitação Medicina", null, tipo), conteudo),
            Repository, Fatos, Termos, Tipos, UnitOfWork, CancellationToken.None);

    protected Task<Result> AtualizarAsync(ModeloFormulario existente, ConteudoDoModeloInput conteudo, string? tipo)
    {
        ArgumentNullException.ThrowIfNull(existente);
        Repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return AtualizarModeloFormularioCommandHandler.Handle(
            new AtualizarModeloFormularioCommand(existente.Id, new DescritivoDoModeloInput("Habilitação Medicina", null, tipo), conteudo),
            Repository, Fatos, Termos, Tipos, UnitOfWork, CancellationToken.None);
    }

    /// <summary>O modelo gravado com o certificado como item, antes de qualquer mudança no catálogo.</summary>
    protected static ModeloFormulario Existente(string? tipo)
    {
        ItemDoModelo item = new(
            "CERTIFICADO", 0, "DADOS", new ApresentacaoDoCampo("Certificado", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));
        return ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, tipo,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }

    protected static readonly EtapaDoModelo Secao = new("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", null, null, null);
    protected static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    protected static ConteudoDoModeloInput Conteudo(params FatoColetadoInput[] itens) => new(
        "Habilitação",
        [
            EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaEtapaFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir(),
        ],
        itens,
        [],
        []);

    protected static ConteudoDoModeloInput ComComposicao(params FatoColetadoInput[] campos) =>
        Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos = [EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao("DADOS")
                .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(campos).Construir()],
        };

    protected static FatoColetadoInput Campo(string fato, int ordem, string tipo = "SIM_OU_NAO") => Montar(fato, ordem, tipo).NaSecao(null).Construir();

    protected static FatoColetadoInput Item(string fato, int ordem, string tipo = "SIM_OU_NAO") => Montar(fato, ordem, tipo).Construir();

    /// <summary>O item na seção <c>DADOS</c>, ainda sem fechar, para o teste que lhe dá regras.</summary>
    private protected static EntradaDoFatoColetadoDeTeste Montar(string fato, int ordem, string tipo = "SIM_OU_NAO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).DoTipo(tipo).NaSecao("DADOS");

    protected static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    protected static FatoCandidato Declarado(
        string codigo, TipoDoDado dominio = TipoDoDado.SimOuNao, FormatoTexto? formato = null, EscopoFato escopo = EscopoFato.Candidato) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, null, formato, "INSCRICAO", escopo, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
}
