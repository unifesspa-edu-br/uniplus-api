namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

public sealed class RegistroDecisaoAcessoTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Uma decisão permitida gera um registro por inscrição, com a permissão e a sensibilidade da origem")]
    public void ParaCadaResource_GeraUmRegistroPorId()
    {
        Guid fernanda = Guid.CreateVersion7();
        Guid caroline = Guid.CreateVersion7();
        Guid isabella = Guid.CreateVersion7();
        Guid processo = Guid.CreateVersion7();
        RegistroDecisaoAcesso origem = Permitido(Resource(ClassificacaoDeProtecao.Pessoal, processoId: processo));

        IReadOnlyList<RegistroDecisaoAcesso> registros = origem.ParaCadaResource("inscricao", [fernanda, caroline, isabella]);

        registros.Select(static r => r.ResourceId).Should().BeEquivalentTo([fernanda, caroline, isabella]);
        registros.Should().OnlyContain(r =>
            r.ResourceTipo == "inscricao"
            && r.Permissao == Permissao
            && r.Sensibilidade == "pessoal"
            && r.ProcessoId == processo
            && r.AtorSubject == "sub-opaco-123");
    }

    [Theory(DisplayName = "O registro leva o client OIDC do contexto; sem ele, ou em branco, vai nulo e a decisão não muda")]
    [InlineData("selecao-web", "selecao-web")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void De_ClienteOidcVemDoContexto(string? azp, string? esperado)
    {
        ResourceContext resource = Resource(ClassificacaoDeProtecao.Pessoal);
        AuthorizationRequestContext requisicao = AuthorizationRequestContext.From(
            "req-1244", Agora, Authorization.Enums.OrigemRequisicao.Api, clienteOidc: azp).Value!;

        RegistroDecisaoAcesso registro = RegistroDecisaoAcesso.De(
            Sujeito(concessoes: Concessao()), Requisito(resource.Sensibilidade), resource, requisicao,
            AuthorizationDecision.Permitir(Concessao()));

        registro.ClienteOidc.Should().Be(esperado);
        registro.Permitido.Should().BeTrue();
    }

    [Fact(DisplayName = "Outra execução, ou outra pessoa, é outro acesso, mesmo com o mesmo identificador de correlação")]
    public void Identidade_DependeDaExecucaoEDoAtor()
    {
        ResourceContext resource = Resource(ClassificacaoDeProtecao.Pessoal, unidadeProprietariaId: Guid.CreateVersion7());
        Guid execucao = Guid.CreateVersion7();

        string basica = Identidade(resource, execucao, "sub-opaco-123");

        Identidade(resource, execucao, "sub-opaco-123").Should().Be(basica);
        Identidade(resource, Guid.CreateVersion7(), "sub-opaco-123").Should().NotBe(basica);
        Identidade(resource, execucao, "outra-pessoa").Should().NotBe(basica);
        Identidade(Resource(ClassificacaoDeProtecao.Pessoal, unidadeProprietariaId: Guid.CreateVersion7()), execucao, "sub-opaco-123")
            .Should().NotBe(basica, "a unidade do resource também distingue o acesso");
    }

    [Fact(DisplayName = "A revelação de um campo é sempre de um resource só: não gera registro por resource")]
    public void ParaCadaResource_ComFatoRevelado_Recusa()
    {
        ResourceContext resource = ResourceContext.From(
            "inscricao", ClassificacaoDeProtecao.Identificador, resourceId: Guid.CreateVersion7(), fatoCodigo: "CPF").Value!;

        Action acao = () => Permitido(resource).ParaCadaResource("inscricao", [Guid.CreateVersion7()]);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A identidade é injetiva: campos que juntos formariam o mesmo texto são acessos diferentes")]
    public void Identidade_ParAmbiguo_ENaoColide()
    {
        ResourceContext resource = Resource(ClassificacaoDeProtecao.Pessoal);
        Guid execucao = Guid.CreateVersion7();

        string a = IdentidadeDe(resource, execucao, "https://idp/x|y", "z");
        string b = IdentidadeDe(resource, execucao, "https://idp/x", "y|z");

        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "Com resource de inscrição, cada registro nomeia a inscrição")]
    public void ParaCadaResource_DeInscricao_NomeiaAInscricao()
    {
        Guid inscricao = Guid.CreateVersion7();

        IReadOnlyList<RegistroDecisaoAcesso> registros = Permitido(Resource(ClassificacaoDeProtecao.Pessoal))
            .ParaCadaResource("inscricao", [inscricao], resourceEhInscricao: true);

        registros.Should().ContainSingle().Which.InscricaoId.Should().Be(inscricao);
    }

    [Fact(DisplayName = "O mecanismo grava a classificação baixa que o chamador registrar")]
    public void Registro_ClassificacaoPublica_FicaGravada()
    {
        RegistroDecisaoAcesso registro = Permitido(Resource(ClassificacaoDeProtecao.Publico));

        registro.Sensibilidade.Should().Be("publico");
    }

    [Fact(DisplayName = "A decisão negada não gera registro por inscrição")]
    public void ParaCadaResource_Negada_Recusa()
    {
        RegistroDecisaoAcesso negada = RegistroDecisaoAcesso.De(
            Sujeito(), Requisito(), Resource(), Requisicao(), AuthorizationDecision.Negar(Authorization.Enums.MotivoNegativa.SemConcessaoAplicavel));

        Action acao = () => negada.ParaCadaResource("inscricao", [Guid.CreateVersion7()]);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Theory(DisplayName = "O registro grava o resultado no valor canônico; o impedimento por inscrição é impedido, e não negado")]
    [InlineData(null, "permitido")]
    [InlineData(Authorization.Enums.MotivoNegativa.SemConcessaoAplicavel, "negado")]
    [InlineData(Authorization.Enums.MotivoNegativa.ImpedidoPorInscricaoNoProcesso, "impedido")]
    public void De_ResultadoSerializaNoValorCanonico(Authorization.Enums.MotivoNegativa? motivo, string esperado)
    {
        AuthorizationDecision decisao = motivo is { } negativa
            ? AuthorizationDecision.Negar(negativa)
            : AuthorizationDecision.Permitir(Concessao());

        RegistroDecisaoAcesso registro = RegistroDecisaoAcesso.De(Sujeito(), Requisito(), Resource(), Requisicao(), decisao);

        using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(registro, Json));
        json.RootElement.GetProperty("resultado").GetString().Should().Be(esperado);
        registro.Permitido.Should().Be(esperado == "permitido");
    }

    [Fact(DisplayName = "O registro da revelação leva o código do fato e a ocorrência")]
    public void Registro_Revelacao_LevaOFatoEAOcorrencia()
    {
        ResourceContext resource = ResourceContext.From(
            "inscricao", ClassificacaoDeProtecao.Identificador, resourceId: Guid.CreateVersion7(), fatoCodigo: "CPF", ocorrencia: "membro-1").Value!;

        RegistroDecisaoAcesso registro = Permitido(resource);

        registro.FatoCodigo.Should().Be("CPF");
        registro.Ocorrencia.Should().Be("membro-1");
    }

    [Fact(DisplayName = "Ocorrência sem código do fato é recusada")]
    public void ResourceContext_OcorrenciaSemFato_Recusa()
    {
        Result<ResourceContext> resultado = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Interno, ocorrencia: "membro-1");

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextOcorrenciaInvalida);
    }

    [Fact(DisplayName = "Código do fato em branco é recusado")]
    public void ResourceContext_FatoEmBranco_Recusa()
    {
        Result<ResourceContext> resultado = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Interno, fatoCodigo: " ");

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextFatoInvalido);
    }

    private static string Identidade(ResourceContext resource, Guid execucao, string subject) =>
        IdentidadeDe(resource, execucao, "https://idp.exemplo/realms/unifesspa", subject);

    private static string IdentidadeDe(ResourceContext resource, Guid execucao, string emissor, string subject)
    {
        Authorization.ValueObjects.UsuarioRef usuario = Authorization.ValueObjects.UsuarioRef.From(emissor, subject).Value!;
        return RegistroDecisaoAcesso.De(
            Authorization.Contracts.AuthorizationSubject.From(usuario, "jti-abc").Value!,
            Requisito(resource.Sensibilidade),
            resource,
            AuthorizationRequestContext.From("req-1244", Agora, Authorization.Enums.OrigemRequisicao.Api, execucaoId: execucao).Value!,
            AuthorizationDecision.Permitir(Concessao())).Identidade;
    }

    private static RegistroDecisaoAcesso Permitido(ResourceContext resource) =>
        RegistroDecisaoAcesso.De(
            Sujeito(concessoes: Concessao()),
            Requisito(resource.Sensibilidade),
            resource,
            Requisicao(),
            AuthorizationDecision.Permitir(Concessao()));
}
