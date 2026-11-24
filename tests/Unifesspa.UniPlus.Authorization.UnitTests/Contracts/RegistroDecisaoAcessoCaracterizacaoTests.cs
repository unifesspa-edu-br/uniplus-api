namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que o registro de acesso grava para entradas conhecidas: cada propriedade, a identidade, a chave e o JSON.
/// </summary>
public sealed class RegistroDecisaoAcessoCaracterizacaoTests
{
    private const string Permissao = "selecao:inscricoes:consultar";

    private const string IdentidadeEsperada =
        """
        ["01920000-0000-7000-8000-000000000001","https://idp.exemplo/realms/unifesspa","conta-fixa-1","selecao:inscricoes:consultar","inscricao","01920000-0000-7000-8000-000000000005","CPF","membro-1","01920000-0000-7000-8000-000000000002","01920000-0000-7000-8000-000000000003","01920000-0000-7000-8000-000000000004",true]
        """;

    private const string JsonPermitido =
        """
        {"Instante":"2026-08-22T13:00:00+00:00","AtorEmissor":"https://idp.exemplo/realms/unifesspa","AtorContaId":"conta-fixa-1","RequestId":"req-fixa","ExecucaoId":"01920000-0000-7000-8000-000000000001","Permissao":"selecao:inscricoes:consultar","ResourceTipo":"inscricao","Sensibilidade":"PESSOAL","UnidadeProprietariaId":"01920000-0000-7000-8000-000000000002","ProcessoId":"01920000-0000-7000-8000-000000000003","ChamadaId":"01920000-0000-7000-8000-000000000004","ResourceId":"01920000-0000-7000-8000-000000000005","InscricaoId":"01920000-0000-7000-8000-000000000005","FatoCodigo":"CPF","Ocorrencia":"membro-1","Origem":"api","ClienteOidc":"selecao-web","Resultado":"permitido","Permitido":true,"MotivoNegativa":null,"FonteGrant":"concessao","GrantId":"01920000-0000-7000-8000-000000000006","PerfilId":"01920000-0000-7000-8000-000000000007","VersaoId":"01920000-0000-7000-8000-000000000008","EscopoUnidadeId":"01920000-0000-7000-8000-000000000002"}
        """;

    private const string JsonNegado =
        """
        {"Instante":"2026-08-22T13:00:00+00:00","AtorEmissor":"https://idp.exemplo/realms/unifesspa","AtorContaId":"conta-fixa-1","RequestId":"req-fixa","ExecucaoId":"01920000-0000-7000-8000-000000000001","Permissao":"selecao:inscricoes:consultar","ResourceTipo":"inscricao","Sensibilidade":"PESSOAL","UnidadeProprietariaId":"01920000-0000-7000-8000-000000000002","ProcessoId":"01920000-0000-7000-8000-000000000003","ChamadaId":"01920000-0000-7000-8000-000000000004","ResourceId":"01920000-0000-7000-8000-000000000005","InscricaoId":"01920000-0000-7000-8000-000000000005","FatoCodigo":"CPF","Ocorrencia":"membro-1","Origem":"api","ClienteOidc":"selecao-web","Resultado":"negado","Permitido":false,"MotivoNegativa":"sem_concessao_aplicavel","FonteGrant":null,"GrantId":null,"PerfilId":null,"VersaoId":null,"EscopoUnidadeId":null}
        """;

    private static readonly Guid Execucao = Guid.Parse("01920000-0000-7000-8000-000000000001");
    private static readonly Guid Unidade = Guid.Parse("01920000-0000-7000-8000-000000000002");
    private static readonly Guid Processo = Guid.Parse("01920000-0000-7000-8000-000000000003");
    private static readonly Guid Chamada = Guid.Parse("01920000-0000-7000-8000-000000000004");
    private static readonly Guid Inscricao = Guid.Parse("01920000-0000-7000-8000-000000000005");
    private static readonly Guid Concessao = Guid.Parse("01920000-0000-7000-8000-000000000006");
    private static readonly Guid Perfil = Guid.Parse("01920000-0000-7000-8000-000000000007");
    private static readonly Guid Versao = Guid.Parse("01920000-0000-7000-8000-000000000008");
    private static readonly DateTimeOffset Instante = new(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);

    private static ResourceContext ResourceDaInscricao() =>
        ResourceContext.From(
            "inscricao", ClassificacaoDeProtecao.Interno, Unidade, Processo, Chamada, Inscricao, inscricaoId: Inscricao)
            .Value!.ComCampo("CPF", "membro-1").Value!;

    private static RegistroDecisaoAcesso Registro(AuthorizationDecision decisao) =>
        RegistroDecisaoAcesso.De(
            AuthorizationSubject.From(UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "conta-fixa-1").Value!, "jti-1").Value!,
            PermissionRequirement.From(Permissao, ClassificacaoDeProtecao.Pessoal).Value!,
            ResourceDaInscricao(),
            AuthorizationRequestContext.From("req-fixa", Instante, OrigemRequisicao.Api, execucaoId: Execucao, clienteOidc: "selecao-web").Value!,
            decisao);

    private static RegistroDecisaoAcesso Permitido() =>
        Registro(AuthorizationDecision.Permitir(EffectiveGrant.DaConcessao(
            Permissao, new ConcessaoAtiva(Concessao, Perfil, Versao, "Perfil", Unidade, false, null, [Permissao])).Value!));

    private static RegistroDecisaoAcesso Negado() =>
        Registro(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

    [Fact(DisplayName = "O registro da decisão permitida leva o ator, a requisição, o resource e a concessão usada")]
    public void De_DecisaoPermitida_LevaTodasAsPropriedades()
    {
        RegistroDecisaoAcesso registro = Permitido();

        (registro.AtorEmissor, registro.AtorContaId, registro.Instante, registro.RequestId, registro.ExecucaoId)
            .Should().Be(("https://idp.exemplo/realms/unifesspa", "conta-fixa-1", Instante, "req-fixa", Execucao));
        (registro.Permissao, registro.ResourceTipo, registro.Sensibilidade, registro.Origem, registro.ClienteOidc)
            .Should().Be((Permissao, "inscricao", "PESSOAL", "api", "selecao-web"));
        (registro.UnidadeProprietariaId, registro.ProcessoId, registro.ChamadaId, registro.ResourceId, registro.InscricaoId)
            .Should().Be(((Guid?)Unidade, (Guid?)Processo, (Guid?)Chamada, (Guid?)Inscricao, (Guid?)Inscricao));
        (registro.FatoCodigo, registro.Ocorrencia, registro.Resultado, registro.Permitido)
            .Should().Be(("CPF", "membro-1", ResultadoDoAcesso.Permitido, true));
        (registro.MotivoNegativa, registro.FonteGrant, registro.GrantId, registro.PerfilId, registro.VersaoId, registro.EscopoUnidadeId)
            .Should().Be(((string?)null, (string?)"concessao", (Guid?)Concessao, (Guid?)Perfil, (Guid?)Versao, (Guid?)Unidade));
    }

    [Fact(DisplayName = "O registro da decisão negada leva o motivo e nenhum dado de concessão")]
    public void De_DecisaoNegada_LevaOMotivoESemConcessao()
    {
        RegistroDecisaoAcesso registro = Negado();

        (registro.Resultado, registro.Permitido, registro.MotivoNegativa).Should()
            .Be((ResultadoDoAcesso.Negado, false, (string?)"sem_concessao_aplicavel"));
        (registro.FonteGrant, registro.GrantId, registro.PerfilId, registro.VersaoId, registro.EscopoUnidadeId).Should()
            .Be(((string?)null, (Guid?)null, (Guid?)null, (Guid?)null, (Guid?)null));
        (registro.Permissao, registro.ResourceId, registro.Sensibilidade).Should().Be((Permissao, (Guid?)Inscricao, "PESSOAL"));
    }

    [Fact(DisplayName = "A identidade e a chave do acesso têm valor fixo para uma entrada conhecida")]
    public void IdentidadeEChave_EntradaConhecida_ValorFixo()
    {
        RegistroDecisaoAcesso registro = Permitido();

        registro.Identidade.Should().Be(IdentidadeEsperada);
        registro.Chave.Should().Be("99775d73c03561046f30b49ed013cbe170ba3e3dada034cf5eed4e0c26c41dce");
    }

    [Fact(DisplayName = "O JSON do registro permitido tem os nomes e a ordem de hoje")]
    public void Json_RegistroPermitido_TextoFixo() =>
        JsonSerializer.Serialize(Permitido()).Should().Be(JsonPermitido);

    [Fact(DisplayName = "O JSON do registro negado tem os nomes e a ordem de hoje")]
    public void Json_RegistroNegado_TextoFixo() =>
        JsonSerializer.Serialize(Negado()).Should().Be(JsonNegado);

    [Fact(DisplayName = "Sem a lista de ids, a recusa é de argumento nulo, antes de olhar o estado")]
    public void ParaCadaResource_IdsNulos_RecusaArgumentoNulo()
    {
        Action acao = () => Negado().ParaCadaResource("Tipo_Invalido", null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "A decisão negada recusa por estado antes de conferir o tipo do resource")]
    public void ParaCadaResource_NegadaComTipoInvalido_RecusaOEstado()
    {
        Action acao = () => Negado().ParaCadaResource("Tipo_Invalido", [Inscricao]);

        acao.Should().Throw<InvalidOperationException>().WithMessage("Só a decisão permitida gera registro por resource.");
    }

    [Fact(DisplayName = "A revelação de um campo recusa por estado antes de conferir o tipo do resource")]
    public void ParaCadaResource_RevelacaoComTipoInvalido_RecusaOEstado()
    {
        Action acao = () => Permitido().ParaCadaResource("Tipo_Invalido", [Inscricao]);

        acao.Should().Throw<InvalidOperationException>().WithMessage("A revelação de um campo é sempre de um resource só.");
    }

    [Fact(DisplayName = "O tipo de resource fora do kebab-case é recusado por argumento")]
    public void ParaCadaResource_TipoInvalido_RecusaArgumento()
    {
        Action acao = () => PermitidoSemFato(ResourceContext.From("inscricao", ClassificacaoDeProtecao.Interno).Value!)
            .ParaCadaResource("Tipo_Invalido", [Inscricao]);

        acao.Should().Throw<ArgumentException>().WithParameterName("resourceTipo");
    }

    [Fact(DisplayName = "Cada registro por resource troca o tipo e o id, herda o resto e repete a inscrição só se o resource é a inscrição")]
    public void ParaCadaResource_DecisaoPermitida_TrocaTipoEId()
    {
        RegistroDecisaoAcesso origem = PermitidoSemFato(
            ResourceContext.From("processo", ClassificacaoDeProtecao.Interno, Unidade, inscricaoId: Inscricao).Value!);
        Guid outra = Guid.Parse("01920000-0000-7000-8000-000000000009");

        IReadOnlyList<RegistroDecisaoAcesso> comoItem = origem.ParaCadaResource("item-de-inscricao", [outra, outra]);
        IReadOnlyList<RegistroDecisaoAcesso> comoInscricao = origem.ParaCadaResource("inscricao", [outra], resourceEhInscricao: true);

        (comoItem.Count, comoItem[0].ResourceTipo, comoItem[0].ResourceId, comoItem[0].InscricaoId).Should()
            .Be((1, "item-de-inscricao", (Guid?)outra, (Guid?)Inscricao));
        (comoInscricao[0].ResourceId, comoInscricao[0].InscricaoId, comoInscricao[0].UnidadeProprietariaId).Should()
            .Be(((Guid?)outra, (Guid?)outra, (Guid?)Unidade));
    }

    private static RegistroDecisaoAcesso PermitidoSemFato(ResourceContext resource) =>
        RegistroDecisaoAcesso.De(
            AuthorizationSubject.From(UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "conta-fixa-1").Value!, "jti-1").Value!,
            PermissionRequirement.From(Permissao, ClassificacaoDeProtecao.Pessoal).Value!,
            resource,
            AuthorizationRequestContext.From("req-fixa", Instante, OrigemRequisicao.Api).Value!,
            AuthorizationDecision.Permitir(EffectiveGrant.PorTitularidade(Permissao).Value!));
}
