namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.ConteudosDeModeloParaTeste;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A gravação do modelo de formulário: o código novo é único, a corrida de índice único e a de
/// concorrência otimista (xmin) viram recusa, e a edição recusada não deixa rastreamento para o
/// próximo salvamento do outbox.
/// </summary>
public sealed class GravacaoDoModeloTests
{
    private const string IndiceUnicoDoCodigo = "ux_modelos_formulario_codigo";

    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly ITermoConsentimentoReader _termos = Substitute.For<ITermoConsentimentoReader>();
    private readonly ITipoProcessoReader _tipos = Substitute.For<ITipoProcessoReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public GravacaoDoModeloTests()
    {
        FatoCandidato certificado = Declarado("CERTIFICADO");
        Aprovar(certificado);
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [certificado]);
        _termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact(DisplayName = "O modelo novo é adicionado e salvo, e o resultado traz o identificador dele")]
    public async Task Criar_ConteudoValido_AdicionaESalva()
    {
        ModeloFormulario? gravado = null;
        await _repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarAsync();

        resultado.Value.Should().Be(gravado!.Id);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O código que já existe, de modelo desativado inclusive, é recusado sem adicionar nem salvar")]
    public async Task Criar_CodigoJaExiste_RecusaSemGravar()
    {
        _repository.CodigoExisteAsync("HABILITACAO_MEDICINA", Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarAsync();

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ModeloFormulario>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Perder a corrida do índice único do código descarta a inserção e devolve código já existente")]
    public async Task Criar_CorridaDoIndiceUnicoDoCodigo_DescartaERecusa()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDeUnicidade(IndiceUnicoDoCodigo));

        Result<Guid> resultado = await CriarAsync();

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A violação de outro índice único não é a do código: propaga sem descartar")]
    public async Task Criar_ViolacaoDeOutroIndice_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDeUnicidade("ix_de_outra_tabela"));

        Func<Task> criar = CriarAsync;

        await criar.Should().ThrowAsync<DbUpdateException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Modelo inexistente é recusado na edição, sem salvar")]
    public async Task Atualizar_ModeloInexistente_RecusaSemSalvar()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModeloFormulario?)null);

        Result resultado = await AtualizarAsync(Guid.NewGuid(), Conteudo(Item("CERTIFICADO", 0)));

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A edição recusada pelo catálogo deixa o modelo rastreado como estava, descarta o rastreamento e não salva")]
    public async Task Atualizar_RecusadaPeloCatalogo_NaoMudaOModeloEDescartaSemSalvar()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        string? nomeNoDescarte = null;
        _unitOfWork.When(u => u.DescartarAlteracoesNaoSalvas()).Do(_ => nomeNoDescarte = existente.Nome);

        Result resultado = await AtualizarAsync(existente.Id, Conteudo(Item("NAO_EXISTE", 0)), nome: "Habilitação Medicina Revisada");

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("conteudo.itens[0].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido));
        nomeNoDescarte.Should().Be("Habilitação Medicina", "a recusa chega antes de qualquer mudança no agregado");
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desativar o modelo ativo o desativa e salva")]
    public async Task Desativar_ModeloAtivo_DesativaESalva()
    {
        ModeloFormulario existente = ExistenteAtivo();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await DesativarModeloFormularioCommandHandler.Handle(
            new DesativarModeloFormularioCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        existente.Ativo.Should().BeFalse();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ativar o modelo desativado o ativa e salva")]
    public async Task Ativar_ModeloDesativado_AtivaESalva()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtivarModeloFormularioCommandHandler.Handle(
            new AtivarModeloFormularioCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        existente.Ativo.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Modelo inexistente é recusado na ativação, sem salvar")]
    public async Task Ativar_ModeloInexistente_RecusaSemSalvar()
    {
        Result resultado = await AtivarModeloFormularioCommandHandler.Handle(
            new AtivarModeloFormularioCommand(Guid.NewGuid()), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida de concorrência (xmin) ao desativar descarta o rastreamento e devolve conflito")]
    public async Task Desativar_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        ModeloFormulario existente = ExistenteAtivo();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await DesativarModeloFormularioCommandHandler.Handle(
            new DesativarModeloFormularioCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A falha de outra natureza ao salvar a edição propaga sem descartar")]
    public async Task Desativar_FalhaDeOutraNatureza_Propaga()
    {
        ModeloFormulario existente = ExistenteAtivo();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha sintética de teste"));

        Func<Task> desativar = () => DesativarModeloFormularioCommandHandler.Handle(
            new DesativarModeloFormularioCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        await desativar.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    private static ModeloFormulario ExistenteAtivo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        existente.Ativar().IsSuccess.Should().BeTrue();
        return existente;
    }

    private static DbUpdateException ViolacaoDeUnicidade(string indice) =>
        new("violação sintética de teste", PostgresExceptionFactory.Create("23505", indice));

    private Task<Result<Guid>> CriarAsync() =>
        CriarModeloFormularioCommandHandler.Handle(
            new CriarModeloFormularioCommand("HABILITACAO_MEDICINA", EntradaDeOutraFaseObrigatoria, new DescritivoDoModeloInput("Habilitação Medicina", null, null), Conteudo(Item("CERTIFICADO", 0))),
            _repository, _fatos, _termos, _tipos, _unitOfWork, CancellationToken.None);

    private Task<Result> AtualizarAsync(Guid id, ConteudoDoModeloInput conteudo, string nome = "Habilitação Medicina") =>
        AtualizarModeloFormularioCommandHandler.Handle(
            new AtualizarModeloFormularioCommand(id, new DescritivoDoModeloInput(nome, null, null), conteudo),
            _repository, _fatos, _termos, _tipos, _unitOfWork, CancellationToken.None);
}
