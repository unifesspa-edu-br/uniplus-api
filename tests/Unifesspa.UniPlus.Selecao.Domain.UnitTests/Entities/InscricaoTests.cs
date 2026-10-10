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
    };

    private static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();

    private static readonly RespostasAdmitidas Admitidas = new(DefinicoesDasRespostas, new HashSet<Guid> { OfertaDoProcesso });

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

    [Fact(DisplayName = "Submeter pela primeira vez gera o número, muda a situação e emite InscricaoSubmetidaEvent")]
    public void Submeter_PrimeiraVez_GeraNumeroEEmiteEventoDeSubmissao()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
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
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        inscricao.ClearDomainEvents();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "999999", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-def",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.NumeroInscricao.Should().Be("000123", "o número gerado só vale se ainda não existir");
        inscricao.Comprovantes.Should().HaveCount(2, "a correção soma um comprovante, nunca substitui");
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCorrigidaEvent>();
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
            "000123", OfertaDoProcesso, respostas, SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
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
            "000123", OfertaDoProcesso, Respostas(), grupos, SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
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

    [Fact(DisplayName = "Submeter recusa o fato derivado informado pelo candidato")]
    public void Submeter_FatoDerivadoInformadoPeloCandidato_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement("PA"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, respostas, SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
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
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, derivados, DefinicoesDosDerivados, "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        inscricao.Derivados.Should().ContainKey("UF_RESIDENCIA");
        inscricao.Respostas.Should().NotContainKey("UF_RESIDENCIA");
        resultado.Value!.Derivados.Should().ContainKey("UF_RESIDENCIA");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL").And.NotContainKey("UF_RESIDENCIA");
    }

    [Fact(DisplayName = "Salvar depois de submeter mantém a definição das respostas derivadas")]
    public void Salvar_DepoisDeSubmeter_MantemADefinicaoDosDerivados()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> derivados = new(StringComparer.Ordinal)
        {
            ["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement("PA"),
        };
        inscricao.Submeter(
            "000123", OfertaDoProcesso, Respostas(), SemGrupos(), SemAceites(), Admitidas, derivados, DefinicoesDosDerivados, "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        inscricao.Salvar(null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Derivados.Should().ContainKey("UF_RESIDENCIA");
        inscricao.DefinicoesDasRespostas.Should().ContainKey("UF_RESIDENCIA", "o derivado continua gravado e precisa da definição para virar linha");
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
            "000123", null, Respostas(), SemGrupos(), SemAceites(), Admitidas, SemDerivados(), DefinicoesDosDerivados, "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.OfertaObrigatoria);
    }

    private static Inscricao NovaInscricao() =>
        Inscricao.Iniciar(Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario).Value!;

    private static DateTimeOffset Instante() => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, JsonElement> Respostas() => new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
    };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    private static List<AceiteDeTermo> SemAceites() => [];

    private static Dictionary<string, JsonElement> SemDerivados() => new(StringComparer.Ordinal);
}
