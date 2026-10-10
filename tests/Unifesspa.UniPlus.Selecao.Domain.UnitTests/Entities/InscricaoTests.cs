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
    private static readonly HashSet<string> SemIdentificador = new(StringComparer.Ordinal);

    [Fact(DisplayName = "Iniciar fixa a versão do formulário e nasce em Rascunho, revisão 1")]
    public void Iniciar_FixaVersaoENasceRascunho()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario, SemIdentificador);

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
            Processo, PersonasDeTeste.FernandaDrumond, "", "https://issuer", VersaoFormulario, SemIdentificador);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.SubObrigatorio);
    }

    [Fact(DisplayName = "Iniciar sem emissor é recusado")]
    public void Iniciar_SemEmissor_RetornaFalha()
    {
        Result<Inscricao> resultado = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "", VersaoFormulario, SemIdentificador);

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
            null, Respostas(), SemGrupos(), SemAceites(), PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoRequerida);
    }

    [Fact(DisplayName = "Salvar com If-Match vencido é recusado, sem mutar nada")]
    public void Salvar_IfMatchVencido_RetornaFalhaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        string etagOriginal = inscricao.ETag;

        Result resultado = inscricao.Salvar(
            Guid.NewGuid(), Respostas(), SemGrupos(), SemAceites(), PrecondicaoIfMatch.DeTags(["\"etag-velho\""]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        inscricao.ETag.Should().Be(etagOriginal, "a recusa não pode avançar a revisão");
        inscricao.OfertaCursoId.Should().BeNull();
    }

    [Fact(DisplayName = "Salvar com If-Match correto grava e avança a revisão")]
    public void Salvar_IfMatchCorreto_GravaEAvancaRevisao()
    {
        Inscricao inscricao = NovaInscricao();
        Guid oferta = Guid.NewGuid();

        Result resultado = inscricao.Salvar(
            oferta, Respostas(), SemGrupos(), SemAceites(), PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
        inscricao.OfertaCursoId.Should().Be(oferta);
        inscricao.Revisao.Should().Be(2);
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar sem o número de documento no corpo preserva o valor gravado")]
    public void Salvar_SemDocumentoNoCorpo_PreservaValorGravado()
    {
        HashSet<string> identificador = new(StringComparer.Ordinal) { "RG_NUMERO" };
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario, identificador).Value!;

        Dictionary<string, JsonElement> comRg = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
        };
        inscricao.Salvar(null, comRg, SemGrupos(), SemAceites(), PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Dictionary<string, JsonElement> semRg = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };
        inscricao.Salvar(null, semRg, SemGrupos(), SemAceites(), PrecondicaoIfMatch.DeTags([inscricao.ETag]))
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
            "000123", null, Respostas(), SemGrupos(), SemAceites(), "sha256-abc",
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
            "000123", null, Respostas(), SemGrupos(), SemAceites(), "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        inscricao.ClearDomainEvents();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "999999", null, Respostas(), SemGrupos(), SemAceites(), "sha256-def",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.NumeroInscricao.Should().Be("000123", "o número gerado só vale se ainda não existir");
        inscricao.Comprovantes.Should().HaveCount(2, "a correção soma um comprovante, nunca substitui");
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCorrigidaEvent>();
    }

    [Fact(DisplayName = "O comprovante nunca carrega número de documento, mesmo que a inscrição carregue")]
    public void Submeter_Comprovante_NaoCarregaDocumento()
    {
        HashSet<string> identificador = new(StringComparer.Ordinal) { "RG_NUMERO" };
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario, identificador).Value!;

        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", null, respostas, SemGrupos(), SemAceites(), "sha256-abc",
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Value!.Respostas.Should().NotContainKey("RG_NUMERO");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O comprovante carrega os grupos declarados, sem o documento de uma ocorrência")]
    public void Submeter_Comprovante_CarregaGruposSemDocumentoDaOcorrencia()
    {
        HashSet<string> identificador = new(StringComparer.Ordinal) { "RG_NUMERO" };
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario, identificador).Value!;

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
            "000123", null, Respostas(), grupos, SemAceites(), "sha256-abc",
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
            respostas, SemGrupos(), SemAceites(), SemIdentificador,
            revisao: 3, criadoEm, atualizadoEm);

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

    private static Inscricao NovaInscricao() =>
        Inscricao.Iniciar(Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario, SemIdentificador).Value!;

    private static DateTimeOffset Instante() => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, JsonElement> Respostas() => new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
    };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    private static List<AceiteDeTermo> SemAceites() => [];
}
