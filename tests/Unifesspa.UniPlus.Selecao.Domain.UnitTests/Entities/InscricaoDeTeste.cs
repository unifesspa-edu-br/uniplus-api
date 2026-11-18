namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class InscricaoDeTeste
{
    internal static readonly Guid Processo = Guid.CreateVersion7();

    internal static readonly Guid VersaoFormulario = Guid.CreateVersion7();

    internal static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas = new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["DESEJA_NOME_SOCIAL"] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false),
        ["RG_NUMERO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["PARENTESCO"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["TELEFONE"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false, Formato: "TELEFONE"),
        ["RENDA_MENSAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        ["SEXO_CIVIL"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["IDENTIDADE_GENERO"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false, CifraIndividual: true),
        ["ENDERECO_RESIDENCIAL"] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
    };

    internal static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();

    internal const string TermoLgpd = "TERMO_LGPD";

    internal const string TermoVeracidade = "TERMO_VERACIDADE";

    internal const string HashDoTermoLgpd = "hash-lgpd-v2";

    internal const string HashDoTermoVeracidade = "hash-veracidade-v1";

    internal static readonly Guid VersaoDoTermoLgpd = Guid.CreateVersion7();

    internal static readonly Guid VersaoDoTermoVeracidade = Guid.CreateVersion7();

    internal static readonly RespostasAdmitidas Admitidas = new(
        DefinicoesDasRespostas,
        new HashSet<string>(StringComparer.Ordinal) { "NOME_SOCIAL", "CPF", "DESEJA_NOME_SOCIAL", "RG_NUMERO", "TELEFONE", "SEXO_CIVIL", "IDENTIDADE_GENERO", "ENDERECO_RESIDENCIAL" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = new HashSet<string>(StringComparer.Ordinal) { "PARENTESCO", "TELEFONE", "RG_NUMERO" },
            ["FONTES_DE_RENDA"] = new HashSet<string>(StringComparer.Ordinal) { "RENDA_MENSAL" },
        },
        new HashSet<Guid> { OfertaDoProcesso },
        new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)
        {
            [TermoLgpd] = new(VersaoDoTermoLgpd, HashDoTermoLgpd),
            [TermoVeracidade] = new(VersaoDoTermoVeracidade, HashDoTermoVeracidade),
        });

    internal static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDosDerivados = new(StringComparer.Ordinal)
    {
        ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
    };

    internal static Inscricao NovaInscricaoEnviada()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(), SemGrupos(), SemAceites()), Admitidas,
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante()).IsSuccess.Should().BeTrue();
        inscricao.ClearDomainEvents();
        return inscricao;
    }

    internal static RespostasAdmitidas AdmitidasComNome(string? nomeDoLogin) =>
        Admitidas with
        {
            Definicoes = new Dictionary<string, DefinicaoDaResposta>(DefinicoesDasRespostas, StringComparer.Ordinal)
            {
                ["NOME"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            },
            DoNivelSuperior = new HashSet<string>(Admitidas.DoNivelSuperior, StringComparer.Ordinal) { "NOME" },
            NomeCivilDoLogin = nomeDoLogin,
        };

    internal static Result SalvarComNome(string? nomeDoLogin, params (string Codigo, string Valor)[] respostas)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(respostas), SemGrupos(), SemAceites()), AdmitidasComNome(nomeDoLogin), PrecondicaoIfMatch.DeTags([inscricao.ETag]));
    }

    internal static RespostasAdmitidas AdmitidasComNomeSocialDaConta(string? nomeSocialDaConta) =>
        Admitidas with { NomeSocialDoLogin = nomeSocialDaConta };

    internal static Dictionary<string, JsonElement> RespostasDeNomeSocial(bool deseja, string? nomeSocial = null)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(deseja),
        };
        if (nomeSocial is not null)
        {
            respostas["NOME_SOCIAL"] = JsonSerializer.SerializeToElement(nomeSocial);
        }

        return respostas;
    }

    internal static Result SalvarComNomeSocial(string? nomeSocialDaConta, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), AdmitidasComNomeSocialDaConta(nomeSocialDaConta), PrecondicaoIfMatch.DeTags([inscricao.ETag]));
    }

    internal static Result<ComprovanteInscricao> SubmeterComNomeSocial(
        string? nomeSocialDaConta, Dictionary<string, JsonElement> respostas, ResultadoDaAvaliacaoDaInscricao? avaliacao = null)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), respostas, SemGrupos(), SemAceites()), AdmitidasComNomeSocialDaConta(nomeSocialDaConta),
            new ApuracaoDoEnvio(avaliacao ?? FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
    }

    internal static ResultadoDaAvaliacaoDaInscricao ComModalidades(SituacaoDasModalidades situacao, params ModalidadeDeConcorrencia[] itens) =>
        FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new ModalidadesDeConcorrencia(
                true, situacao, itens, [], new DependenciasDaAvaliacao(["CONCORRER_PCD", "COMPOSICAO_FAMILIAR.RENDA"], ["COMPOSICAO_FAMILIAR"])),
        };

    internal static Result<ComprovanteInscricao> SubmeterCom(Inscricao inscricao, ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(), SemGrupos(), SemAceites()), Admitidas,
            new ApuracaoDoEnvio(avaliacao, SemDerivados(), DefinicoesDosDerivados, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

    internal static readonly Guid SegundaOfertaDoProcesso = Guid.CreateVersion7();

    internal static readonly RespostasAdmitidas AdmitidasComDuasOpcoes = Admitidas with
    {
        OfertasDeCurso = new HashSet<Guid> { OfertaDoProcesso, SegundaOfertaDoProcesso },
        NumeroDeOpcoesDeCurso = 2,
    };

    internal static Result SalvarOpcoes(Inscricao inscricao, Guid? primeira, Guid? segunda, RespostasAdmitidas admitidas) =>
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(primeira, segunda), Respostas(), SemGrupos(), SemAceites()), admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

    internal static Result<ComprovanteInscricao> SubmeterOpcoes(
        Inscricao inscricao, Guid? primeira, Guid? segunda, ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(primeira, segunda), Respostas(), SemGrupos(), SemAceites()), AdmitidasComDuasOpcoes,
            new ApuracaoDoEnvio(avaliacao, SemDerivados(), DefinicoesDosDerivados, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

    internal static ResultadoDaAvaliacaoDaInscricao ComModalidadesPorOpcao(
        ModalidadeDeConcorrencia[] daPrimeira, SituacaoDasModalidades situacaoDaSegunda, params ModalidadeDeConcorrencia[] daSegunda)
    {
        DependenciasDaAvaliacao dependencias = new(["CONCORRER_PCD"], []);
        return FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new ModalidadesDeConcorrencia(true, SituacaoDasModalidades.Concorre, daPrimeira, [], dependencias),
            ModalidadesSegundaOpcao = new ModalidadesDeConcorrencia(true, situacaoDaSegunda, daSegunda, [], dependencias),
        };
    }

    internal static Dictionary<string, JsonElement> EnderecoResidencial(string? cep, string? cidade, string? complemento = null) => new(StringComparer.Ordinal)
    {
        ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
        {
            cep,
            complemento,
            cidade = cidade is null ? null : new { codigoIbge = "5002704", nome = cidade, uf = "MS" },
        }),
    };

    internal static readonly Guid VersaoRetificada = Guid.CreateVersion7();

    internal static Inscricao NovaInscricao() =>
        Inscricao.Iniciar(Processo, PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", VersaoFormulario).Value!;

    internal static DateTimeOffset Instante() => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal static Dictionary<string, JsonElement> Respostas() => new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
    };

    internal static Dictionary<string, JsonElement> Respostas(params (string Codigo, string Valor)[] itens) =>
        itens.ToDictionary(static i => i.Codigo, static i => JsonSerializer.SerializeToElement(i.Valor), StringComparer.Ordinal);

    internal static Dictionary<string, JsonElement> ComTelefone(string telefone) => new(StringComparer.Ordinal)
    {
        ["TELEFONE"] = JsonSerializer.SerializeToElement(telefone),
    };

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    internal static List<AceiteDeTermo> SemAceites() => [];

    internal static Dictionary<string, JsonElement> SemDerivados() => new(StringComparer.Ordinal);

    internal static PreenchimentoDoCandidato Recebido(IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyList<AceiteDeTermo> aceites) =>
        PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), respostas, grupos, aceites);

    internal static ApuracaoDoEnvio Apuracao(ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        new(avaliacao, SemDerivados(), DefinicoesDosDerivados, []);

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> FamiliaComMembro(params (string Codigo, string Valor)[] respostas) =>
        new(StringComparer.Ordinal) { ["COMPOSICAO_FAMILIAR"] = [new("membro-1", Respostas(respostas))] };
}
