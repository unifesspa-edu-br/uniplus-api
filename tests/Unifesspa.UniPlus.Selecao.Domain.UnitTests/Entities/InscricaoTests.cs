namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Ciclo de vida do agregado <see cref="Inscricao"/> (issue #1858): a validação dinâmica do
/// formulário (obrigatórios, restrições, impedimentos, termos, Geo) é da issue #1859 e não é
/// exercitada aqui — só o que o próprio agregado garante: fixação da versão, precondição de
/// concorrência, geração do número na primeira submissão, comprovante e eventos.
/// </summary>
public sealed class InscricaoTests
{
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid VersaoFormulario = Guid.CreateVersion7();
    private static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas = new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["DESEJA_NOME_SOCIAL"] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false),
        ["RG_NUMERO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["PARENTESCO"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["TELEFONE"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false, Formato: "TELEFONE"),
        ["RENDA_MENSAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
    };

    private static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();

    private static readonly RespostasAdmitidas Admitidas = new(
        DefinicoesDasRespostas,
        new HashSet<string>(StringComparer.Ordinal) { "NOME_SOCIAL", "CPF", "DESEJA_NOME_SOCIAL", "RG_NUMERO", "TELEFONE" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = new HashSet<string>(StringComparer.Ordinal) { "PARENTESCO", "TELEFONE", "RG_NUMERO" },
            ["FONTES_DE_RENDA"] = new HashSet<string>(StringComparer.Ordinal) { "RENDA_MENSAL" },
        },
        new HashSet<Guid> { OfertaDoProcesso });

    private static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDosDerivados = new(StringComparer.Ordinal)
    {
        ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
    };

    [Fact(DisplayName = "Iniciar fixa a versão do formulário e nasce em Rascunho, revisão 1")]
    public void Iniciar_FixaVersaoENasceRascunho()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario);

        resultado.IsSuccess.Should().BeTrue();
        Inscricao inscricao = resultado.Value!;
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.VersaoFormularioId.Should().Be(VersaoFormulario);
        inscricao.NumeroInscricao.Should().BeNull("o número só nasce na primeira submissão");
        inscricao.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Iniciar sem sub é recusado — o token precisa do claim")]
    public void Iniciar_SemSub_RetornaFalha()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "", "https://issuer", VersaoFormulario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.SubObrigatorio);
    }

    [Fact(DisplayName = "Iniciar sem emissor é recusado")]
    public void Iniciar_SemEmissor_RetornaFalha()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "", VersaoFormulario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.EmissorObrigatorio);
    }

    [Fact(DisplayName = "ConferirDono recusa sub diferente do gravado")]
    public void ConferirDono_SubDiferente_RetornaFalha()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.ConferirDono("outro-sub")!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        inscricao.ConferirDono("sub-1").Should().BeNull("o mesmo sub que iniciou é o dono");
    }

    [Fact(DisplayName = "Salvar sem If-Match é recusado — a precondição é obrigatória")]
    public void Salvar_SemIfMatch_RetornaFalha()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoRequerida);
    }

    [Fact(DisplayName = "Salvar com If-Match vencido é recusado, sem mutar nada")]
    public void Salvar_IfMatchVencido_RetornaFalhaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        string etagOriginal = inscricao.ETag;

        Result resultado = inscricao.Salvar(
            Guid.NewGuid(), Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags(["\"etag-velho\""]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        inscricao.ETag.Should().Be(etagOriginal, "a recusa não pode avançar a revisão");
        inscricao.OfertaCursoId.Should().BeNull();
    }

    [Fact(DisplayName = "Salvar com If-Match correto grava e avança a revisão")]
    public void Salvar_IfMatchCorreto_GravaEAvancaRevisao()
    {
        Inscricao inscricao = NovaInscricao();
        Guid oferta = OfertaDoProcesso;

        Result resultado = inscricao.Salvar(
            oferta, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
        inscricao.OfertaCursoId.Should().Be(oferta);
        inscricao.Revisao.Should().Be(2);
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar sem o número de documento no corpo preserva o valor gravado")]
    public void Salvar_SemDocumentoNoCorpo_PreservaValorGravado()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, JsonElement> comRg = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
        };
        inscricao.Salvar(null, comRg, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Dictionary<string, JsonElement> semRg = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };
        inscricao.Salvar(null, semRg, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Respostas.Should().ContainKey("RG_NUMERO")
            .WhoseValue.GetString().Should().Be("12.868.927-4", "ausência no corpo nunca apaga");
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar grava o telefone só com os dígitos; o texto fora do formato fica como veio, para o envio recusá-lo")]
    public void Salvar_Telefone_GravaNaFormaCanonica()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(null, ComTelefone("(94) 3322-1234"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Respostas["TELEFONE"].GetString().Should().Be("9433221234");

        inscricao.Salvar(null, ComTelefone("(94) 3322"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Respostas["TELEFONE"].GetString().Should().Be("(94) 3322");
    }

    [Fact(DisplayName = "Submeter grava e assina no comprovante o telefone só com os dígitos, também dentro de um grupo")]
    public void Submeter_Telefone_ComprovanteCobreAFormaCanonica()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaDeGrupoInscricao("1", ComTelefone("(94) 99123-4567"))],
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, ComTelefone("(94) 3322-1234"), grupos, SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos,
            SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        ComprovanteInscricao comprovante = resultado.Value!;
        inscricao.Respostas["TELEFONE"].GetString().Should().Be("9433221234");
        comprovante.Respostas["TELEFONE"].GetString().Should().Be("9433221234");
        comprovante.Grupos["COMPOSICAO_FAMILIAR"].Should().ContainSingle()
            .Which.Respostas["TELEFONE"].GetString().Should().Be("94991234567");
        comprovante.IntegridadeConfere().Should().BeTrue("o hash é calculado sobre o que o comprovante guarda");
    }

    [Fact(DisplayName = "Submeter pela primeira vez gera o número, muda a situação e emite InscricaoSubmetidaEvent")]
    public void Submeter_PrimeiraVez_GeraNumeroEEmiteEventoDeSubmissao()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
        inscricao.NumeroInscricao.Should().Be("000123");
        inscricao.Comprovantes.Should().ContainSingle();
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoSubmetidaEvent>();
    }

    [Fact(DisplayName = "Submeter de novo (correção) preserva o número, gera comprovante novo e emite InscricaoCorrigidaEvent")]
    public void Submeter_Correcao_PreservaNumeroEEmiteEventoDeCorrecao()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        inscricao.ClearDomainEvents();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "999999", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.NumeroInscricao.Should().Be("000123", "o número gerado só vale se ainda não existir");
        inscricao.Comprovantes.Should().HaveCount(2, "a correção soma um comprovante, nunca substitui");
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCorrigidaEvent>();
    }

    [Fact(DisplayName = "Depois do envio, salvar rascunho é recusado sem mutar: só um novo envio muda a inscrição")]
    public void Salvar_DepoisDoEnvio_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.Salvar(
            OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "O comprovante nunca carrega número de documento, mesmo que a inscrição carregue")]
    public void Submeter_Comprovante_NaoCarregaDocumento()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Value!.Respostas.Should().NotContainKey("RG_NUMERO");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O comprovante carrega os grupos declarados, sem o documento de uma ocorrência")]
    public void Submeter_Comprovante_CarregaGruposSemDocumentoDaOcorrencia()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["RG_NUMERO"] = JsonSerializer.SerializeToElement("25.062.156-3"),
                    ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
                }),
            ],
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), grupos, SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias = resultado.Value!.Grupos.Should()
            .ContainKey("COMPOSICAO_FAMILIAR").WhoseValue;
        ocorrencias.Should().ContainSingle().Which.Id.Should().Be("membro-1");
        ocorrencias.Single().Respostas.Should().NotContainKey("RG_NUMERO");
        ocorrencias.Single().Respostas.Should().ContainKey("PARENTESCO");
    }

    [Fact(DisplayName = "Reidratar reconstrói o estado persistido, inclusive CreatedAt/UpdatedAt e a revisão")]
    public void Reidratar_ReconstroiEstadoPersistido()
    {
        Guid id = Guid.CreateVersion7();
        Guid oferta = Guid.NewGuid();
        DateTimeOffset criadoEm = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset atualizadoEm = new(2026, 9, 2, 11, 0, 0, TimeSpan.Zero);
        Dictionary<string, JsonElement> respostas = Respostas();

        Inscricao inscricao = Inscricao.Reidratar(
            id, Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer",
            SituacaoInscricao.Submetida, VersaoFormulario, oferta, "000123",
            respostas, SemDerivados(), SemGrupos(), SemAceites(), DefinicoesDasRespostas,
            3, criadoEm, atualizadoEm);

        inscricao.Id.Should().Be(id);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
        inscricao.OfertaCursoId.Should().Be(oferta);
        inscricao.NumeroInscricao.Should().Be("000123");
        inscricao.Revisao.Should().Be(3);
        inscricao.ETag.Should().Be($"\"{id}:3\"");
        inscricao.CreatedAt.Should().Be(criadoEm);
        inscricao.UpdatedAt.Should().Be(atualizadoEm);
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
        inscricao.Comprovantes.Should().BeEmpty("Reidratar não recarrega o histórico de comprovantes no agregado");
    }

    [Fact(DisplayName = "Salvar recusa resposta de fato que o formulário não coleta, sem mutar nada")]
    public void Salvar_FatoForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CAMPO_INVENTADO"] = JsonSerializer.SerializeToElement("valor"),
        };

        Result resultado = inscricao.Salvar(null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("CAMPO_INVENTADO");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Salvar recusa valor que não cabe no tipo do fato")]
    public void Salvar_ValorForaDoTipo_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement("sim"),
        };

        Result resultado = inscricao.Salvar(null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ValorForaDoTipo);
    }

    [Fact(DisplayName = "Salvar recusa duas ocorrências do mesmo grupo com o mesmo id")]
    public void Salvar_IdDeOcorrenciaRepetido_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            ],
        };

        Result resultado = inscricao.Salvar(null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Salvar recusa o grupo que o formulário não tem, sem mutar nada")]
    public void Salvar_GrupoForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["GRUPO_INVENTADO"] = [new OcorrenciaDeGrupoInscricao("1", new Dictionary<string, JsonElement>(StringComparer.Ordinal))],
        };

        Result resultado = inscricao.Salvar(null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("GRUPO_INVENTADO");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.GrupoForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
        inscricao.Grupos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Salvar recusa na ocorrência a resposta de fato que não é subitem do grupo")]
    [InlineData("RENDA_MENSAL", "subitem de outro grupo")]
    [InlineData("NOME_SOCIAL", "fato de nível superior")]
    public void Salvar_FatoForaDoGrupo_RecusaSemMutar(string fato, string origem)
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [fato] = JsonSerializer.SerializeToElement("valor"),
                }),
            ],
        };

        Result resultado = inscricao.Salvar(null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle(origem).Which.Field.Should().Be($"COMPOSICAO_FAMILIAR.membro-1.{fato}");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoGrupo);
        inscricao.Revisao.Should().Be(1);
        inscricao.Grupos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Salvar recusa no nível superior o fato que só existe como subitem de grupo, sem mutar nada")]
    public void Salvar_SubitemDeGrupoNoNivelSuperior_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RENDA_MENSAL"] = JsonSerializer.SerializeToElement("1500"),
        };

        Result resultado = inscricao.Salvar(null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("RENDA_MENSAL");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
        inscricao.Revisao.Should().Be(1);
        inscricao.Respostas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Salvar admite no nível superior e na ocorrência o fato que o formulário coleta nos dois lugares")]
    public void Salvar_FatoNosDoisNiveis_Admite()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("123456"),
        };
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["RG_NUMERO"] = JsonSerializer.SerializeToElement("654321"),
                }),
            ],
        };

        inscricao.Salvar(null, respostas, grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Submeter recusa no nível superior o fato que só existe como subitem de grupo")]
    public void Submeter_SubitemDeGrupoNoNivelSuperior_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RENDA_MENSAL"] = JsonSerializer.SerializeToElement("1500"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("RENDA_MENSAL");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
    }

    [Fact(DisplayName = "Salvar com nulo apaga a resposta que o candidato limpou")]
    public void Salvar_Nulo_ApagaResposta()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        Dictionary<string, JsonElement> limpa = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement<string?>(null),
        };

        inscricao.Salvar(null, limpa, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Respostas.Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar preserva o grupo omitido do corpo e substitui o grupo enviado, mesmo vazio")]
    public void Salvar_GrupoOmitido_PreservaOGravado()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> comMembro = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["PARENTESCO"] = JsonSerializer.SerializeToElement("PAI_OU_MAE"),
                }),
            ],
        };
        inscricao.Salvar(null, Respostas(), comMembro, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        inscricao.Salvar(null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Should().ContainSingle(static o => o.Id == "membro-1",
            "o grupo ausente do corpo vale o que já está gravado");

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupoVazio = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [],
        };
        inscricao.Salvar(null, Respostas(), grupoVazio, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Should().BeEmpty("o grupo enviado substitui o gravado, mesmo sem ocorrências");
    }

    [Fact(DisplayName = "Submeter recusa o fato derivado informado pelo candidato")]
    public void Submeter_FatoDerivadoInformadoPeloCandidato_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement("PA"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoFormulario);
    }

    [Fact(DisplayName = "Submeter separa os derivados das respostas, na inscrição e no comprovante")]
    public void Submeter_SeparaDerivadosDasRespostas()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> derivados = new(StringComparer.Ordinal)
        {
            ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement("PA"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, derivados, DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        inscricao.Derivados.Should().ContainKey("UF_RESIDENCIA");
        inscricao.Respostas.Should().NotContainKey("UF_RESIDENCIA");
        resultado.Value!.Derivados.Should().ContainKey("UF_RESIDENCIA");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL").And.NotContainKey("UF_RESIDENCIA");
    }

    [Theory(DisplayName = "A resposta do CPF é aceita só quando é o CPF do dono, com ou sem pontuação")]
    [InlineData("69959836371", true)]
    [InlineData("699.598.363-71", true)]
    [InlineData("33257350376", false)]
    public void Salvar_CpfDaResposta_TemDeSerODoDono(string cpfRespondido, bool aceito)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario).Value!;
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CPF"] = JsonSerializer.SerializeToElement(cpfRespondido),
        };

        Result resultado = inscricao.Salvar(null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().Be(aceito);
        if (!aceito)
        {
            resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
                e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
        }
    }

    [Fact(DisplayName = "Uma oferta de curso que não é do processo é recusada já no rascunho")]
    public void Salvar_OfertaForaDoProcesso_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            Guid.CreateVersion7(), Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoId" && e.Error.Code == InscricaoErrorCodes.OfertaForaDoProcesso);
    }

    [Fact(DisplayName = "Submeter sem oferta, num processo com ofertas, é recusado; o rascunho aceita")]
    public void Submeter_SemOfertaNumProcessoComOfertas_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.OfertaObrigatoria);
    }

    private static ResultadoDaAvaliacaoDaInscricao ComModalidades(SituacaoDasModalidades situacao, params ModalidadeDeConcorrencia[] itens) =>
        FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new ModalidadesDeConcorrencia(
                true, situacao, itens, [], new DependenciasDaAvaliacao(["CONCORRER_PCD", "COMPOSICAO_FAMILIAR.RENDA"], ["COMPOSICAO_FAMILIAR"])),
        };

    private static Result<ComprovanteInscricao> SubmeterCom(Inscricao inscricao, ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, avaliacao, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

    [Fact(DisplayName = "Com a modalidade derivada e nenhuma aplicável, o envio é recusado em cada campo e grupo de que ela depende, sem mudar a inscrição")]
    public void Submeter_NenhumaModalidade_RecusaNosCamposQueADecidem()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.NaoConcorre));

        resultado.Errors.Select(static e => e.Field).Should().Equal("CONCORRER_PCD", "COMPOSICAO_FAMILIAR");
        resultado.Errors.Should().AllSatisfy(static e => e.Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade));
        resultado.Errors[0].Error.Message.Should().EndWith("consulte o edital.");
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Revisao.Should().Be(revisao);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a modalidade derivada ainda indecidida, o envio é recusado em vez de sair sem modalidade")]
    public void Submeter_ModalidadeNaoDecidida_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.FaltaResponder));

        resultado.Errors.Should().ContainSingle().Which.Should().Be(new FieldError(
            "MODALIDADE", resultado.Errors[0].Error));
        resultado.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.ModalidadeNaoDecidida);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "O envio grava os códigos da modalidade como derivado, na ordem das ofertadas, e o comprovante registra a seção com o nome")]
    public void Submeter_ComModalidade_GravaODerivadoEOComprovante()
    {
        Inscricao inscricao = NovaInscricao();

        ComprovanteInscricao comprovante = SubmeterCom(inscricao, ComModalidades(
            SituacaoDasModalidades.Concorre,
            new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0),
            new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 3))).Value!;

        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC", "LI_EP");
        inscricao.DefinicoesDasRespostas["MODALIDADE"].Should().Be(
            new DefinicaoDaResposta(TipoDoValorDeResposta.Codigo, Multivalorado: true, Identificador: false));
        comprovante.Modalidades.Should().Equal(
            new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0), new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 3));
        comprovante.Derivados.Should().NotContainKey("MODALIDADE", "a seção das modalidades já a registra, com o nome");
        comprovante.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "Sem a modalidade derivada, o envio não grava modalidade e o comprovante não tem a seção")]
    public void Submeter_SemDerivacaoDaModalidade_ComprovanteSemASecao()
    {
        Inscricao inscricao = NovaInscricao();

        ComprovanteInscricao comprovante = SubmeterCom(inscricao, FormularioDeTeste.AvaliacaoSemOcultos).Value!;

        inscricao.Derivados.Should().NotContainKey("MODALIDADE");
        comprovante.Modalidades.Should().BeNull();
    }

    [Fact(DisplayName = "A correção sem modalidade aplicável é recusada e não muda a inscrição já enviada nem o comprovante dela")]
    public void Submeter_CorrecaoSemModalidade_NaoMudaOEnvioAnterior()
    {
        Inscricao inscricao = NovaInscricao();
        ComprovanteInscricao primeiro = SubmeterCom(
            inscricao, ComModalidades(SituacaoDasModalidades.Concorre, new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0))).Value!;
        int revisao = inscricao.Revisao;

        Result<ComprovanteInscricao> correcao = SubmeterCom(inscricao, ComModalidades(SituacaoDasModalidades.NaoConcorre));

        correcao.IsFailure.Should().BeTrue();
        inscricao.Revisao.Should().Be(revisao);
        inscricao.Comprovantes.Should().ContainSingle().Which.Should().BeSameAs(primeiro);
        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
    }

    private static Inscricao NovaInscricao() =>
        Inscricao.Iniciar(Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario).Value!;

    private static DateTimeOffset Instante() => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, JsonElement> Respostas() => new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
    };

    private static Dictionary<string, JsonElement> ComTelefone(string telefone) => new(StringComparer.Ordinal)
    {
        ["TELEFONE"] = JsonSerializer.SerializeToElement(telefone),
    };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    private static List<AceiteDeTermo> SemAceites() => [];

    private static Dictionary<string, JsonElement> SemDerivados() => new(StringComparer.Ordinal);
}
