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

    private static OperacaoSobreOPerfil Operacao(Perfil perfil, out UnidadeDeTrabalho unidade)
    {
        unidade = new UnidadeDeTrabalho { Falha = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException() };

        return new OperacaoSobreOPerfil(
            new PerfisNaMemoria(perfil), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1)));
    }
}
