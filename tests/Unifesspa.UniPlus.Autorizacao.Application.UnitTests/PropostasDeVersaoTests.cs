namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class PropostasDeVersaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Pessoal = "selecao:inscricoes:ler-dados-pessoais";
    private const string Descricao = "Descrição válida.";
    private const string Motivo = "Permissão concedida por engano.";

    private static readonly CatalogoDeTeste Catalogo = new();

    [Fact(DisplayName = "Sem autor identificado nada é gravado")]
    public async Task Propor_SemAutor_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, null, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(perfil.Id, Dados("Analista", Consultar), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Dados inválidos são recusados por inteiro, sem gravar")]
    public async Task Propor_DadosInvalidos_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, new ProporVersaoRequest(" ", Descricao, [Consultar], PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento, null, null), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NomeInvalido);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Categoria da remoção fora das previstas é recusada como remoção inválida")]
    public async Task Propor_CategoriaInvalida_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", [Consultar], Motivo, "inexistente"), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.RemocaoInvalida);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Proposta do perfil vigente sem remoção grava a pendente e devolve o envio, sem mexer na vigente")]
    public async Task Propor_SemRemocao_GravaAPendente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", Consultar, Publicar), CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        gravacao.Resultado.Value!.Perfil.Should().BeNull();
        perfil.VersaoPendente!.Id.Should().Be(gravacao.Resultado.Value.Envio!.VersaoId);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Perfil inexistente devolve não encontrado")]
    public async Task Propor_PerfilInexistente_NaoEncontrado()
    {
        CenarioDaGovernancaDaVersao cenario = new(PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo), PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            Guid.NewGuid(), Dados("Analista", Consultar), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Nome de versão que outro perfil usa é recusado e nada é gravado")]
    public async Task Propor_NomeDeOutroPerfil_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Perfil outro = PerfisDeTeste.Vigente("Publicador", Descricao, [Consultar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [], outrosPerfis: [outro]);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Publicador", Consultar), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NomeDuplicado);
        cenario.Unidade.Gravacoes.Should().Be(0);
        cenario.Unidade.Descartes.Should().Be(1);
    }

    [Fact(DisplayName = "Acrescentar permissão pessoal a perfil com concessão é recusado")]
    public async Task Propor_AmpliacaoComConcessao_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [], temConcessao: true);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", Consultar, Pessoal), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AmpliacaoExigeProtecaoDeDados);
    }

    [Fact(DisplayName = "A proposta que remove e acrescenta grava a remoção na hora e devolve o envio da pendente só com o acréscimo")]
    public async Task Propor_RemoveEAcrescenta_GravaARemocaoEAPendente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Pessoal], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", [Consultar, Publicar], Motivo, "incidente"), CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        gravacao.Resultado.Value!.Envio!.VersaoId.Should().Be(perfil.VersaoPendente!.Id);
        perfil.VersaoVigente!.Origem.Should().Be(OrigemDaVersao.Remocao);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
        perfil.VersaoPendente.Codigos().Should().Equal(Consultar, Publicar);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "A proposta só com remoção vale na hora, devolve o perfil e não emite envio de documento")]
    public async Task Propor_SoRemocao_DevolveOPerfil()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", [Consultar], Motivo, null), CancellationToken.None);

        gravacao.Resultado.Value!.Envio.Should().BeNull();
        gravacao.Resultado.Value.Perfil!.Id.Should().Be(perfil.Id);
        perfil.VersaoPendente.Should().BeNull();
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
    }

    [Fact(DisplayName = "A proposta que remove sem motivo é recusada com o erro de motivo e nada é gravado")]
    public async Task Propor_RemoveSemMotivo_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", [Consultar], null, null), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        cenario.Unidade.Gravacoes.Should().Be(0);
        perfil.VersaoVigente!.Numero.Should().Be(1);
    }

    [Fact(DisplayName = "A pendente recusada desfaz a remoção: nada é gravado e a vigente fica como estava")]
    public async Task Propor_PendenteRecusada_DesfazARemocao()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Pessoal], Catalogo);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [], temConcessao: true);

        GravacaoDaOperacao<ResultadoDaPropostaDto> gravacao = await cenario.Propostas.ProporAsync(
            perfil.Id, Dados("Analista", [Consultar, Publicar], Motivo, null), CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AcrescimoComConcessaoAtiva);
        cenario.Unidade.Gravacoes.Should().Be(0);
        cenario.Unidade.Descartes.Should().Be(1);
        perfil.VersaoVigente!.Numero.Should().Be(1);
        perfil.VersaoPendente.Should().BeNull();
    }

    private static ProporVersaoRequest Dados(string nome, params string[] permissoes) =>
        Dados(nome, permissoes.Length == 0 ? [Consultar] : permissoes, null, null);

    private static ProporVersaoRequest Dados(string nome, string[] permissoes, string? motivo, string? categoria) =>
        new(nome, Descricao, permissoes, PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento, motivo, categoria);
}
