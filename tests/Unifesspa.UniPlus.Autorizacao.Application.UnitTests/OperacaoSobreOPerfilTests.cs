namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class OperacaoSobreOPerfilTests
{
    private const string Consultar = "selecao:processos:consultar";

    [Fact(DisplayName = "Sem chave de idempotência o conflito de concorrência propaga, para o middleware responder 409")]
    public async Task SemChave_ConflitoDeConcorrencia_Propaga()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = Operacao(perfil, out UnidadeDeTrabalho unidade);

        Func<Task> chamada = () => operacao.ExecutarSemChaveAsync(
            perfil.Id, (_, _) => Task.FromResult(Result<bool>.Success(true)), CancellationToken.None);

        await chamada.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>();
        unidade.Descartes.Should().Be(0);
    }

    [Fact(DisplayName = "A operação com avisos e sem chave, como a rotina da janela, deixa o conflito de concorrência propagar")]
    public async Task ComAvisosSemChave_ConflitoDeConcorrencia_Propaga()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = Operacao(perfil, out UnidadeDeTrabalho unidade);

        Func<Task> chamada = () => operacao.ExecutarComAvisosSemChaveAsync(
            perfil.Id, (_, _) => Task.FromResult(new DesfechoDaOperacao<bool>(Result<bool>.Success(true))), CancellationToken.None);

        await chamada.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>();
        unidade.Descartes.Should().Be(0);
    }

    [Fact(DisplayName = "Com chave de idempotência o conflito de concorrência vira recusa e descarta o rastreamento")]
    public async Task ComChave_ConflitoDeConcorrencia_DevolveRecusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = Operacao(perfil, out UnidadeDeTrabalho unidade);

        Result<bool> resultado = await operacao.ExecutarAsync(
            perfil.Id, (_, _) => Task.FromResult(Result<bool>.Success(true)), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartes.Should().Be(1);
    }

    [Fact(DisplayName = "O perfil que não existe devolve não encontrado, sem rodar a operação nem gravar")]
    public async Task ComAvisos_PerfilInexistente_NaoEncontrado()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = OperacaoSemFalha(perfil, out UnidadeDeTrabalho unidade);
        Func<Perfil, ContextoDaOperacao, Task<DesfechoDaOperacao<bool>>> naoDeveRodar = (_, _) => throw new InvalidOperationException("A operação não deveria rodar.");

        GravacaoDaOperacao<bool> gravacao = await operacao.ExecutarComAvisosAsync(Guid.NewGuid(), naoDeveRodar, CancellationToken.None);

        gravacao.Resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O desfecho com sucesso se grava uma vez")]
    public async Task ComAvisos_Sucesso_Grava()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = OperacaoSemFalha(perfil, out UnidadeDeTrabalho unidade);

        GravacaoDaOperacao<bool> gravacao = await operacao.ExecutarComAvisosAsync(
            perfil.Id, (_, _) => Task.FromResult(new DesfechoDaOperacao<bool>(Result<bool>.Success(true))), CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        (unidade.Gravacoes, unidade.Descartes).Should().Be((1, 0));
    }

    [Fact(DisplayName = "O desfecho recusado descarta o rastreamento e não grava")]
    public async Task ComAvisos_Recusa_Descarta()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = OperacaoSemFalha(perfil, out UnidadeDeTrabalho unidade);

        GravacaoDaOperacao<bool> gravacao = await operacao.ExecutarComAvisosAsync(
            perfil.Id, (_, _) => Task.FromResult(new DesfechoDaOperacao<bool>(Result<bool>.Failure(PerfilHandlerErros.AutorNaoIdentificado()))), CancellationToken.None);

        gravacao.Resultado.IsFailure.Should().BeTrue();
        (unidade.Gravacoes, unidade.Descartes).Should().Be((0, 1));
    }

    [Fact(DisplayName = "O desfecho recusado que pede para gravar mesmo assim grava")]
    public async Task ComAvisos_RecusaQueGravaMesmoNaFalha_Grava()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = OperacaoSemFalha(perfil, out UnidadeDeTrabalho unidade);

        GravacaoDaOperacao<bool> gravacao = await operacao.ExecutarComAvisosAsync(
            perfil.Id,
            (_, _) => Task.FromResult(new DesfechoDaOperacao<bool>(Result<bool>.Failure(PerfilHandlerErros.AutorNaoIdentificado()), GravarMesmoNaFalha: true)),
            CancellationToken.None);

        gravacao.Resultado.IsFailure.Should().BeTrue();
        (unidade.Gravacoes, unidade.Descartes).Should().Be((1, 0));
    }

    [Fact(DisplayName = "A operação recusada que encontrou o prazo da pendente vencido grava a expiração mesmo assim")]
    public async Task ComAvisos_RecusaComPrazoVencido_GravaAExpiracao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        OperacaoSobreOPerfil operacao = OperacaoSemFalha(perfil, out UnidadeDeTrabalho unidade, PerfisDeTeste.Instante.AddYears(1));

        GravacaoDaOperacao<bool> gravacao = await operacao.ExecutarComAvisosAsync(
            perfil.Id, (_, _) => Task.FromResult(new DesfechoDaOperacao<bool>(Result<bool>.Failure(PerfilHandlerErros.AutorNaoIdentificado()))), CancellationToken.None);

        gravacao.Resultado.IsFailure.Should().BeTrue();
        perfil.VersaoPendente.Should().BeNull("o prazo da pendente venceu e a expiração se gravou");
        (unidade.Gravacoes, unidade.Descartes).Should().Be((1, 0));
    }

    private static OperacaoSobreOPerfil OperacaoSemFalha(Perfil perfil, out UnidadeDeTrabalho unidade, DateTimeOffset? agora = null)
    {
        unidade = new UnidadeDeTrabalho();

        return new OperacaoSobreOPerfil(
            new PerfisNaMemoria(perfil), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(agora ?? PerfisDeTeste.Instante.AddHours(1)));
    }

    private static OperacaoSobreOPerfil Operacao(Perfil perfil, out UnidadeDeTrabalho unidade)
    {
        unidade = new UnidadeDeTrabalho { Falha = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException() };

        return new OperacaoSobreOPerfil(
            new PerfisNaMemoria(perfil), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1)));
    }
}
