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
        ["SEXO_CIVIL"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["IDENTIDADE_GENERO"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false, CifraIndividual: true),
        ["ENDERECO_RESIDENCIAL"] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
    };

    private static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();

    private const string TermoLgpd = "TERMO_LGPD";
    private const string TermoVeracidade = "TERMO_VERACIDADE";
    private const string HashDoTermoLgpd = "hash-lgpd-v2";
    private const string HashDoTermoVeracidade = "hash-veracidade-v1";
    private static readonly Guid VersaoDoTermoLgpd = Guid.CreateVersion7();
    private static readonly Guid VersaoDoTermoVeracidade = Guid.CreateVersion7();

    private static readonly RespostasAdmitidas Admitidas = new(
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

    [Theory(DisplayName = "ConferirDono sem sub no token recusa com SubObrigatorio, sem exceção")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConferirDono_SemSub_RecusaComSubObrigatorio(string? sub)
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.ConferirDono(sub)!.Code.Should().Be(InscricaoErrorCodes.SubObrigatorio);
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
            null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoRequerida);
    }

    [Fact(DisplayName = "Salvar com If-Match vencido é recusado, sem mutar nada")]
    public void Salvar_IfMatchVencido_RetornaFalhaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        string etagOriginal = inscricao.ETag;

        Result resultado = inscricao.Salvar(
            Guid.NewGuid(), null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags(["\"etag-velho\""]));

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
            oferta, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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
        inscricao.Salvar(null, null, comRg, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Dictionary<string, JsonElement> semRg = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };
        inscricao.Salvar(null, null, semRg, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Respostas.Should().ContainKey("RG_NUMERO")
            .WhoseValue.GetString().Should().Be("12.868.927-4", "ausência no corpo nunca apaga");
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar grava o telefone só com os dígitos; o texto fora do formato fica como veio, para o envio recusá-lo")]
    public void Salvar_Telefone_GravaNaFormaCanonica()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(null, null, ComTelefone("(94) 3322-1234"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Respostas["TELEFONE"].GetString().Should().Be("9433221234");

        inscricao.Salvar(null, null, ComTelefone("(94) 3322"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
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
            "000123", OfertaDoProcesso, null, ComTelefone("(94) 3322-1234"), grupos, SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos,
            SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);
        inscricao.ClearDomainEvents();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "999999", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.Salvar(
            OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        inscricao.Revisao.Should().Be(revisao);
    }

    private static Inscricao NovaInscricaoEnviada()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []).IsSuccess.Should().BeTrue();
        inscricao.ClearDomainEvents();
        return inscricao;
    }

    [Fact(DisplayName = "Cancelar a inscrição enviada grava data, ator e motivo, mantém o número e emite InscricaoCanceladaEvent")]
    public void Cancelar_Enviada_GravaDataAtorEMotivoEMantemONumero()
    {
        Inscricao inscricao = NovaInscricaoEnviada();
        int revisao = inscricao.Revisao;
        DateTimeOffset instante = Instante();

        Result resultado = inscricao.Cancelar("Mudei de curso", "sub-1", instante, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        inscricao.CanceladaEm.Should().Be(instante);
        inscricao.CanceladaPor.Should().Be("sub-1");
        inscricao.MotivoDoCancelamento.Should().Be("Mudei de curso");
        inscricao.NumeroInscricao.Should().Be("000123");
        inscricao.Revisao.Should().Be(revisao + 1);
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCanceladaEvent>();
    }

    [Theory(DisplayName = "Cancelar sem motivo grava \"a pedido do candidato\"")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Cancelar_SemMotivo_GravaOMotivoPadrao(string? motivo)
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        inscricao.Cancelar(motivo, "sub-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();

        inscricao.MotivoDoCancelamento.Should().Be("a pedido do candidato");
    }

    [Fact(DisplayName = "Descartar a inscrição enviada é recusado orientando a cancelar")]
    public void ConferirDescarte_Enviada_OrientaACancelar()
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        DomainError recusa = inscricao.ConferirDescarte(PrecondicaoIfMatch.DeTags([inscricao.ETag]))!;

        recusa.Code.Should().Be(InscricaoErrorCodes.DescarteSoDeRascunho);
        recusa.Message.Should().Contain("cancele a inscrição").And.NotContain("/");
    }

    [Fact(DisplayName = "O rascunho não se cancela: a recusa orienta a descartar e nada muda")]
    public void Cancelar_Rascunho_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.Cancelar(null, "sub-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.CancelamentoSoDeEnviada);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Cancelar com If-Match vencido ou com motivo acima do limite é recusado, sem mutar")]
    public void Cancelar_PrecondicaoOuMotivoInvalidos_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        inscricao.Cancelar(null, "sub-1", Instante(), PrecondicaoIfMatch.DeTags(["\"outro\""])).Error!.Code
            .Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        inscricao.Cancelar(new string('x', Inscricao.TamanhoMaximoDoMotivoDoCancelamento + 1), "sub-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).Error!.Code
            .Should().Be(InscricaoErrorCodes.MotivoDoCancelamentoLongo);

        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
    }

    [Fact(DisplayName = "A inscrição cancelada recusa salvar, enviar, cancelar de novo, descartar e adotar versão")]
    public void Cancelada_NaoSeAltera()
    {
        Inscricao inscricao = NovaInscricaoEnviada();
        inscricao.Cancelar(null, "sub-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        PrecondicaoIfMatch corrente = PrecondicaoIfMatch.DeTags([inscricao.ETag]);
        int revisao = inscricao.Revisao;

        List<DomainError?> recusas =
        [
            inscricao.Salvar(OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, corrente).Error,
            inscricao.Submeter(
                "999999", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
                corrente, Instante(), []).Error,
            inscricao.Cancelar(null, "sub-1", Instante(), corrente).Error,
            inscricao.ConferirDescarte(corrente),
            inscricao.AdotarVersao(Guid.CreateVersion7(), Admitidas).Error,
        ];

        recusas.Should().OnlyContain(e => e != null && e.Code == InscricaoErrorCodes.InscricaoCancelada);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Com o período encerrado nenhuma ação vale; aberto, corrigir e cancelar pedem a enviada e continuar e descartar, o rascunho")]
    public void AcoesPermitidas_DependemDaSituacaoEDoPeriodo()
    {
        Inscricao rascunho = NovaInscricao();
        Inscricao enviada = NovaInscricaoEnviada();
        Inscricao cancelada = NovaInscricaoEnviada();
        cancelada.Cancelar(null, "sub-1", Instante(), PrecondicaoIfMatch.DeTags([cancelada.ETag]));

        enviada.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(false, true, true, false));
        enviada.AcoesPermitidas(inscricoesAbertas: false).Should().Be(new AcoesDaInscricao(false, false, false, false));
        rascunho.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(true, false, false, true));
        rascunho.AcoesPermitidas(inscricoesAbertas: false).Should().Be(new AcoesDaInscricao(false, false, false, false));
        cancelada.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(false, false, false, false));
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
            "000123", OfertaDoProcesso, null, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.Value!.Respostas.Should().NotContainKey("RG_NUMERO");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O comprovante leva o dado de cifra individual à parte, fora do JSON em claro e do hash, e o sexo civil segue em claro")]
    public void Submeter_Comprovante_LevaOCifradoIndividualAParte()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
            ["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO"),
            ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PESSOA_NAO_BINARIA"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        ComprovanteInscricao comprovante = resultado.Value!;
        comprovante.Respostas.Should().NotContainKey("IDENTIDADE_GENERO");
        comprovante.Respostas["SEXO_CIVIL"].GetString().Should().Be("FEMININO");
        comprovante.RespostasSensiveis["IDENTIDADE_GENERO"].GetString().Should().Be("PESSOA_NAO_BINARIA");
        comprovante.IntegridadeConfere().Should().BeTrue();
        inscricao.Respostas["IDENTIDADE_GENERO"].GetString().Should().Be("PESSOA_NAO_BINARIA", "a inscrição guarda o valor; a cifra é da gravação");
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
            "000123", OfertaDoProcesso, null, Respostas(), grupos, SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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
            SituacaoInscricao.Submetida, VersaoFormulario, oferta, null, "000123", null, null, null,
            respostas, SemDerivados(), SemGrupos(), SemAceites(), DefinicoesDasRespostas,
            3, criadoEm, atualizadoEm);

        inscricao.Id.Should().Be(id);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Submetida);
        inscricao.OfertaCursoId.Should().Be(oferta);
        inscricao.NumeroInscricao.Should().Be("000123");
        inscricao.Revisao.Should().Be(3);
        inscricao.ETag.Should().Be($"\"{id}:3:{VersaoFormulario}\"");
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

        Result resultado = inscricao.Salvar(null, null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        Result resultado = inscricao.Salvar(null, null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        Result resultado = inscricao.Salvar(null, null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        Result resultado = inscricao.Salvar(null, null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        Result resultado = inscricao.Salvar(null, null, Respostas(), grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        Result resultado = inscricao.Salvar(null, null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

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

        inscricao.Salvar(null, null, respostas, grupos, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
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
            "000123", OfertaDoProcesso, null, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("RENDA_MENSAL");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
    }

    [Fact(DisplayName = "Salvar com nulo apaga a resposta que o candidato limpou")]
    public void Salvar_Nulo_ApagaResposta()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        Dictionary<string, JsonElement> limpa = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement<string?>(null),
        };

        inscricao.Salvar(null, null, limpa, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
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
        inscricao.Salvar(null, null, Respostas(), comMembro, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        inscricao.Salvar(null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Should().ContainSingle(static o => o.Id == "membro-1",
            "o grupo ausente do corpo vale o que já está gravado");

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupoVazio = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [],
        };
        inscricao.Salvar(null, null, Respostas(), grupoVazio, SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
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
            "000123", OfertaDoProcesso, null, respostas, SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, derivados, DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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

        Result resultado = inscricao.Salvar(null, null, respostas, SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().Be(aceito);
        if (!aceito)
        {
            resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
                e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
        }
    }

    private static RespostasAdmitidas AdmitidasComNome(string? nomeDoLogin) =>
        Admitidas with
        {
            Definicoes = new Dictionary<string, DefinicaoDaResposta>(DefinicoesDasRespostas, StringComparer.Ordinal)
            {
                ["NOME"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            },
            DoNivelSuperior = new HashSet<string>(Admitidas.DoNivelSuperior, StringComparer.Ordinal) { "NOME" },
            NomeCivilDoLogin = nomeDoLogin,
        };

    private static Result SalvarComNome(string? nomeDoLogin, params (string Codigo, string Valor)[] respostas)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Salvar(null, null, Respostas(respostas), SemGrupos(), SemAceites(), AdmitidasComNome(nomeDoLogin), PrecondicaoIfMatch.DeTags([inscricao.ETag]));
    }

    [Fact(DisplayName = "Um NOME diferente do nome do login é recusado ao salvar, no campo NOME")]
    public void Salvar_NomeDiferenteDoLogin_Recusa()
    {
        Result resultado = SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME", "Outra Pessoa"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Fact(DisplayName = "Um NOME diferente do nome do login é recusado ao enviar, no campo NOME")]
    public void Submeter_NomeDiferenteDoLogin_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(("NOME", "Outra Pessoa")), SemGrupos(), SemAceites(), AdmitidasComNome(PersonasDeTeste.NomeFernandaDrumond),
            FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Fact(DisplayName = "Um rascunho com NOME divergente do login, enviado sem NOME, é recusado no campo NOME")]
    public void Submeter_RascunhoComNomeDivergente_SemNomeNoEnvio_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, null, Respostas(("NOME", "Outra Pessoa")), SemGrupos(), SemAceites(), AdmitidasComNome("Outra Pessoa"), PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), AdmitidasComNome(PersonasDeTeste.NomeFernandaDrumond),
            FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Theory(DisplayName = "O NOME igual ao do login é aceito, ignorando espaços nas pontas e repetidos; maiúsculas e acentos contam")]
    [InlineData("Fernanda Laura Nicole Drumond", true)]
    [InlineData("  Fernanda  Laura Nicole   Drumond ", true)]
    [InlineData("FERNANDA LAURA NICOLE DRUMOND", false)]
    [InlineData("Fernánda Laura Nicole Drumond", false)]
    public void Salvar_NomeDoLogin_ComparaSemEspacosSobrando(string nomeEnviado, bool aceito)
    {
        Result resultado = SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME", nomeEnviado));

        resultado.IsSuccess.Should().Be(aceito);
    }

    [Fact(DisplayName = "Sem nome no token, o formulário que coleta NOME recusa no campo NOME")]
    public void Salvar_TokenSemNome_FormularioColetaNome_Recusa()
    {
        Result resultado = SalvarComNome(null, ("NOME", "Fernanda Laura Nicole Drumond"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeAusenteNoLogin);
    }

    [Fact(DisplayName = "Sem nome no token, o formulário que não coleta NOME segue como antes")]
    public void Salvar_TokenSemNome_FormularioNaoColetaNome_Aceita()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Omitir o NOME não é recusado, e o nome social segue livre")]
    public void Salvar_NomeOmitido_NomeSocialLivre_Aceita()
    {
        SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME_SOCIAL", "Ariel Drumond")).IsSuccess.Should().BeTrue();
    }

    private static RespostasAdmitidas AdmitidasComNomeSocialDaConta(string? nomeSocialDaConta) =>
        Admitidas with { NomeSocialDoLogin = nomeSocialDaConta };

    private static Dictionary<string, JsonElement> RespostasDeNomeSocial(bool deseja, string? nomeSocial = null)
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

    private static Result SalvarComNomeSocial(string? nomeSocialDaConta, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Salvar(null, null, respostas, SemGrupos(), SemAceites(), AdmitidasComNomeSocialDaConta(nomeSocialDaConta), PrecondicaoIfMatch.DeTags([inscricao.ETag]));
    }

    private static Result<ComprovanteInscricao> SubmeterComNomeSocial(
        string? nomeSocialDaConta, Dictionary<string, JsonElement> respostas, ResultadoDaAvaliacaoDaInscricao? avaliacao = null)
    {
        Inscricao inscricao = NovaInscricao();
        return inscricao.Submeter(
            "000123", OfertaDoProcesso, null, respostas, SemGrupos(), SemAceites(), AdmitidasComNomeSocialDaConta(nomeSocialDaConta),
            avaliacao ?? FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);
    }

    [Fact(DisplayName = "Um NOME_SOCIAL diferente do nome social da conta é recusado ao salvar, no campo NOME_SOCIAL")]
    public void Salvar_NomeSocialDiferenteDaConta_Recusa()
    {
        Result resultado = SalvarComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Um NOME_SOCIAL diferente do nome social da conta é recusado ao enviar, no campo NOME_SOCIAL")]
    public void Submeter_NomeSocialDiferenteDaConta_Recusa()
    {
        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Ao enviar, o NOME_SOCIAL do rascunho que não é mais o da conta é recusado ainda que o corpo não o repita")]
    public void Submeter_NomeSocialGravadoDivergente_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, null, RespostasDeNomeSocial(true, "Nome Antigo"), SemGrupos(), SemAceites(), AdmitidasComNomeSocialDaConta("Nome Antigo"), PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, new Dictionary<string, JsonElement>(), SemGrupos(), SemAceites(), AdmitidasComNomeSocialDaConta(PersonasDeTeste.NomeSocialFernandaDrumond),
            FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Ao enviar com sim e a conta sem nome social, a recusa orienta cadastrar em Meu perfil ou desmarcar a opção")]
    public void Submeter_SimSemNomeSocialNaConta_RecusaComOrientacao()
    {
        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(null, RespostasDeNomeSocial(true));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialAusenteNaConta
                && e.Error.Message == "Você marcou que deseja usar nome social, mas sua conta não tem nome social cadastrado. Cadastre o nome social em Meu perfil ou desmarque a opção.");
    }

    [Theory(DisplayName = "Ao salvar com sim e a conta sem nome social, o campo vazio ou ausente não é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Salvar_SimSemNomeSocialNaConta_CampoVazio_Aceita(string? nomeSocial)
    {
        SalvarComNomeSocial(null, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Ao salvar com sim e a conta sem nome social, um NOME_SOCIAL preenchido é recusado com a orientação")]
    public void Salvar_SimSemNomeSocialNaConta_CampoPreenchido_Recusa()
    {
        Result resultado = SalvarComNomeSocial(null, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialAusenteNaConta);
    }

    [Theory(DisplayName = "O NOME_SOCIAL igual ao da conta é aceito, ignorando espaços nas pontas e repetidos")]
    [InlineData("Ariel Drumond")]
    [InlineData("  Ariel   Drumond ")]
    public void NomeSocialDaConta_ComparaSemEspacosSobrando(string nomeSocial)
    {
        SalvarComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
        SubmeterComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Com não, a conta com nome social não deixa nome social gravado na inscrição enviada")]
    public void Submeter_NaoDesejaNomeSocial_NaoGravaNomeSocial()
    {
        ResultadoDaAvaliacaoDaInscricao semNomeSocial = FormularioDeTeste.AvaliacaoDoEnvio(new(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["NOME_SOCIAL"] = FatoResolvido.NaoAplicavel() }, [], [], [], []));

        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(
            PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(false, "Outro Nome"), semNomeSocial);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Respostas.Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Uma oferta de curso que não é do processo é recusada já no rascunho")]
    public void Salvar_OfertaForaDoProcesso_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            Guid.CreateVersion7(), null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoId" && e.Error.Code == InscricaoErrorCodes.OfertaForaDoProcesso);
    }

    [Fact(DisplayName = "Submeter sem oferta, num processo com ofertas, é recusado; o rascunho aceita")]
    public void Submeter_SemOfertaNumProcessoComOfertas_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", null, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.OfertaObrigatoria);
    }

    [Fact(DisplayName = "Salvar grava o aceite do termo na versão e no hash do formulário, e aceita a lista incompleta")]
    public void Salvar_AceiteConferido_GravaEAceitaListaIncompleta()
    {
        Inscricao inscricao = NovaInscricao();
        AceiteDeTermo lgpd = new(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd);

        Result resultado = inscricao.Salvar(null, null, Respostas(), SemGrupos(), [lgpd], Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue("o rascunho não exige todos os termos; isso é do veredito de submissão");
        inscricao.Aceites.Should().ContainSingle().Which.Should().Be(lgpd);
    }

    [Fact(DisplayName = "Salvar recusa o aceite de código repetido no caminho do repetido, sem mutar nada")]
    public void Salvar_AceiteRepetido_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new(TermoLgpd, Guid.CreateVersion7(), "hash-forjado"),
        ];

        Result resultado = inscricao.Salvar(null, null, Respostas(), SemGrupos(), aceites, Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteRepetido);
        inscricao.Revisao.Should().Be(1);
        inscricao.Aceites.Should().BeEmpty();
    }

    [Fact(DisplayName = "Salvar recusa o aceite de termo que a versão fixada não exige, sem mutar nada")]
    public void Salvar_AceiteForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new("TERMO_INEXISTENTE", Guid.CreateVersion7(), "hash-qualquer"),
        ];

        Result resultado = inscricao.Salvar(null, null, Respostas(), SemGrupos(), aceites, Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
        inscricao.Aceites.Should().BeEmpty();
    }

    [Theory(DisplayName = "Salvar recusa o aceite cuja versão ou hash não confere com o do termo exigido, sem mutar nada")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Salvar_AceiteDeOutraVersao_RecusaSemMutar(bool versaoDiferente, bool hashDiferente)
    {
        Inscricao inscricao = NovaInscricao();
        AceiteDeTermo aceite = new(
            TermoLgpd,
            versaoDiferente ? Guid.CreateVersion7() : VersaoDoTermoLgpd,
            hashDiferente ? "hash-forjado" : HashDoTermoLgpd);

        Result resultado = inscricao.Salvar(null, null, Respostas(), SemGrupos(), [aceite], Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[0]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteDeOutraVersao);
        inscricao.Revisao.Should().Be(1);
        inscricao.Aceites.Should().BeEmpty();
    }

    [Theory(DisplayName = "Submeter recusa o aceite repetido, fora do formulário ou de outra versão, sem mudar a inscrição nem emitir comprovante")]
    [InlineData(InscricaoErrorCodes.AceiteRepetido)]
    [InlineData(InscricaoErrorCodes.AceiteForaDoFormulario)]
    [InlineData(InscricaoErrorCodes.AceiteDeOutraVersao)]
    public void Submeter_AceiteInvalido_RecusaSemMutar(string codigoEsperado)
    {
        Inscricao inscricao = NovaInscricao();
        AceiteDeTermo valido = new(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd);
        List<AceiteDeTermo> aceites = codigoEsperado switch
        {
            InscricaoErrorCodes.AceiteRepetido => [valido, valido with { HashVersao = "hash-forjado" }],
            InscricaoErrorCodes.AceiteForaDoFormulario => [valido, new("TERMO_INEXISTENTE", Guid.CreateVersion7(), "hash-qualquer")],
            _ => [valido, new(TermoVeracidade, VersaoDoTermoVeracidade, "hash-forjado")],
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), aceites, Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(codigoEsperado);
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Comprovantes.Should().BeEmpty();
        inscricao.Aceites.Should().BeEmpty();
    }

    [Fact(DisplayName = "Submeter grava no comprovante os aceites conferidos com a versão e o hash do formulário")]
    public void Submeter_AceitesConferidos_ComprovanteRegistraOsAceites()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new(TermoVeracidade, VersaoDoTermoVeracidade, HashDoTermoVeracidade),
        ];

        Result<ComprovanteInscricao> resultado = inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), aceites, Admitidas, FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Aceites.Should().Equal(aceites);
    }

    private static ResultadoDaAvaliacaoDaInscricao ComModalidades(SituacaoDasModalidades situacao, params ModalidadeDeConcorrencia[] itens) =>
        FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new ModalidadesDeConcorrencia(
                true, situacao, itens, [], new DependenciasDaAvaliacao(["CONCORRER_PCD", "COMPOSICAO_FAMILIAR.RENDA"], ["COMPOSICAO_FAMILIAR"])),
        };

    private static Result<ComprovanteInscricao> SubmeterCom(Inscricao inscricao, ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        inscricao.Submeter(
            "000123", OfertaDoProcesso, null, Respostas(), SemGrupos(), SemAceites(), Admitidas, avaliacao, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

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

    [Fact(DisplayName = "O documento obrigatório ausente recusa o envio junto das demais recusas, sem mudar a inscrição")]
    public void Submeter_DocumentoObrigatorioAusente_RecusaJuntoDasDemais()
    {
        Inscricao inscricao = NovaInscricao();
        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "CAT", Aplicabilidade.Geral, obrigatorio: true, null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidades(SituacaoDasModalidades.FaltaResponder);
        avaliacao = avaliacao with { Documentos = avaliacao.Documentos with { Ausentes = [new DocumentoAusente([laudo], null)] } };

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, avaliacao);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(
            InscricaoErrorCodes.ModalidadeNaoDecidida, InscricaoErrorCodes.DocumentoObrigatorioAusente);
        resultado.Errors[^1].Field.Should().Be($"documentos.{laudo.Id}");
        inscricao.Situacao.Should().Be(SituacaoInscricao.Rascunho);
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

    private static readonly Guid SegundaOfertaDoProcesso = Guid.CreateVersion7();

    private static readonly RespostasAdmitidas AdmitidasComDuasOpcoes = Admitidas with
    {
        OfertasDeCurso = new HashSet<Guid> { OfertaDoProcesso, SegundaOfertaDoProcesso },
        NumeroDeOpcoesDeCurso = 2,
    };

    private static Result SalvarOpcoes(Inscricao inscricao, Guid? primeira, Guid? segunda, RespostasAdmitidas admitidas) =>
        inscricao.Salvar(primeira, segunda, Respostas(), SemGrupos(), SemAceites(), admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

    private static Result<ComprovanteInscricao> SubmeterOpcoes(
        Inscricao inscricao, Guid? primeira, Guid? segunda, ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        inscricao.Submeter(
            "000123", primeira, segunda, Respostas(), SemGrupos(), SemAceites(), AdmitidasComDuasOpcoes, avaliacao, SemDerivados(), DefinicoesDosDerivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante(), []);

    private static ResultadoDaAvaliacaoDaInscricao ComModalidadesPorOpcao(
        ModalidadeDeConcorrencia[] daPrimeira, SituacaoDasModalidades situacaoDaSegunda, params ModalidadeDeConcorrencia[] daSegunda)
    {
        DependenciasDaAvaliacao dependencias = new(["CONCORRER_PCD"], []);
        return FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Modalidades = new ModalidadesDeConcorrencia(true, SituacaoDasModalidades.Concorre, daPrimeira, [], dependencias),
            ModalidadesSegundaOpcao = new ModalidadesDeConcorrencia(true, situacaoDaSegunda, daSegunda, [], dependencias),
        };
    }

    [Fact(DisplayName = "A 2ª opção é recusada, já no rascunho, quando a versão fixada admite uma opção só")]
    public void Salvar_SegundaOpcaoEmVersaoDeUmaOpcao_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = SalvarOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, Admitidas with { OfertasDeCurso = new HashSet<Guid> { OfertaDoProcesso, SegundaOfertaDoProcesso } });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoNaoAdmitida);
    }

    [Fact(DisplayName = "As recusas das duas opções saem juntas: a 1ª fora do processo e a 2ª igual a ela, também fora")]
    public void Salvar_RecusasDasOpcoesSaemAcumuladas()
    {
        Inscricao inscricao = NovaInscricao();
        Guid foraDoProcesso = Guid.CreateVersion7();

        Result resultado = SalvarOpcoes(inscricao, foraDoProcesso, foraDoProcesso, AdmitidasComDuasOpcoes);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("ofertaCursoId", InscricaoErrorCodes.OfertaForaDoProcesso),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.OfertaForaDoProcesso),
        ]);
    }

    [Fact(DisplayName = "A 2ª opção igual à 1ª é recusada no campo da 2ª")]
    public void Salvar_SegundaOpcaoIgualAPrimeira_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = SalvarOpcoes(inscricao, OfertaDoProcesso, OfertaDoProcesso, AdmitidasComDuasOpcoes);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira);
    }

    [Fact(DisplayName = "Sem a 1ª opção, a 2ª passa no rascunho e é recusada no envio")]
    public void Opcoes_SegundaSemPrimeira_SoERecusadaNoEnvio()
    {
        Inscricao inscricao = NovaInscricao();

        SalvarOpcoes(inscricao, null, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();
        Result<ComprovanteInscricao> envio = SubmeterOpcoes(inscricao, null, SegundaOfertaDoProcesso, FormularioDeTeste.AvaliacaoSemOcultos);

        envio.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoSemPrimeira));
    }

    [Fact(DisplayName = "O envio grava a 2ª opção e as modalidades dela à parte das da 1ª, e o comprovante as congela")]
    public void Submeter_ComSegundaOpcao_GravaAsModalidadesPorOpcao()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ModalidadeDeConcorrencia escolaPublica = new("LI_EP", "Escola pública", 3);

        ComprovanteInscricao comprovante = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([ampla, escolaPublica], SituacaoDasModalidades.Concorre, ampla)).Value!;

        inscricao.OfertaCursoSegundaOpcaoId.Should().Be(SegundaOfertaDoProcesso);
        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC", "LI_EP");
        inscricao.Derivados["MODALIDADE_SEGUNDA_OPCAO"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
        comprovante.OfertaCursoSegundaOpcaoId.Should().Be(SegundaOfertaDoProcesso);
        comprovante.Modalidades.Should().Equal(ampla, escolaPublica);
        comprovante.ModalidadesSegundaOpcao.Should().Equal(ampla);
        comprovante.Derivados.Should().NotContainKey("MODALIDADE_SEGUNDA_OPCAO", "a seção das modalidades da 2ª opção já as registra, com o nome");
        comprovante.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "Quem não concorre a nenhuma modalidade da 2ª opção é recusado no campo dela, mesmo concorrendo na 1ª")]
    public void Submeter_NenhumaModalidadeNaSegundaOpcao_RecusaNoCampoDela()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)], SituacaoDasModalidades.NaoConcorre));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.NenhumaModalidade);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Quem não concorre a nenhuma modalidade da 1ª opção, tendo declarado a 2ª, é recusado com a mensagem da 1ª opção")]
    public void Submeter_NenhumaModalidadeNaPrimeiraOpcaoComSegunda_DizQueEDaPrimeira()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidadesPorOpcao([ampla], SituacaoDasModalidades.Concorre, ampla) with
        {
            Modalidades = new ModalidadesDeConcorrencia(
                true, SituacaoDasModalidades.NaoConcorre, [], [], new DependenciasDaAvaliacao(["CONCORRER_PCD"], [])),
        };

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, avaliacao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "CONCORRER_PCD" && e.Error.Message.Contains("1ª opção de curso", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A modalidade da 2ª opção ainda indecidida e sem campo a indicar é recusada no campo da 2ª, não no da 1ª")]
    public void Submeter_ModalidadeDaSegundaOpcaoNaoDecidida_RecusaNoCampoDela()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)], SituacaoDasModalidades.FaltaResponder));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.ModalidadeNaoDecidida);
    }

    [Fact(DisplayName = "O reenvio com a 2ª opção nula a retira da inscrição, e o novo comprovante sai sem ela")]
    public void Submeter_ReenvioSemSegundaOpcao_RetiraAOpcao()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidadesPorOpcao([ampla], SituacaoDasModalidades.Concorre, ampla);
        SubmeterOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, avaliacao).IsSuccess.Should().BeTrue();

        ComprovanteInscricao reenvio = SubmeterOpcoes(inscricao, OfertaDoProcesso, null, avaliacao).Value!;

        inscricao.OfertaCursoSegundaOpcaoId.Should().BeNull();
        inscricao.Derivados.Should().NotContainKey("MODALIDADE_SEGUNDA_OPCAO");
        reenvio.OfertaCursoSegundaOpcaoId.Should().BeNull();
        reenvio.ModalidadesSegundaOpcao.Should().BeNull();
        inscricao.Comprovantes.Should().HaveCount(2);
    }

    [Theory(DisplayName = "Endereço sem CEP, ou com CEP que não tem 8 dígitos, é recusado ao salvar com a orientação")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7909673")]
    [InlineData("7909673A")]
    public void Salvar_EnderecoSemCepValido_Recusa(string? cep)
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            null, null, EnderecoResidencial(cep, "Campo Grande"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep
                && e.Error.Message.Contains("CEP geral da sua cidade", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "O CEP com a máscara ou sem ela é aceito")]
    [InlineData("79096-730")]
    [InlineData("79096730")]
    public void Salvar_EnderecoComCep_Aceita(string cep)
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            null, null, EnderecoResidencial(cep, "Campo Grande"), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O endereço em branco não é um endereço sem CEP: é a resposta ainda não dada")]
    public void Salvar_EnderecoEmBranco_Aceita()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(
            null, null, EnderecoResidencial(null, null), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O complemento de até 300 caracteres é aceito e o de 301 é recusado, ao salvar e ao enviar")]
    public void Complemento_LimiteDe300Caracteres()
    {
        Inscricao salvando = NovaInscricao();
        Result noLimite = salvando.Salvar(
            null, null, EnderecoResidencial("79096730", "Campo Grande", new string('a', 300)), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([salvando.ETag]));
        Result acima = salvando.Salvar(
            null, null, EnderecoResidencial("79096730", "Campo Grande", new string('a', 301)), SemGrupos(), SemAceites(), Admitidas, PrecondicaoIfMatch.DeTags([salvando.ETag]));

        noLimite.IsSuccess.Should().BeTrue();
        acima.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoComplementoLongo);

        Inscricao enviando = NovaInscricao();
        Result<ComprovanteInscricao> enviado = enviando.Submeter(
            "000123", OfertaDoProcesso, null, EnderecoResidencial("79096730", "Campo Grande", new string('a', 301)), SemGrupos(), SemAceites(), Admitidas,
            FormularioDeTeste.AvaliacaoSemOcultos, SemDerivados(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([enviando.ETag]), Instante(), []);

        enviado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.EnderecoComplementoLongo);
    }

    private static Dictionary<string, JsonElement> EnderecoResidencial(string? cep, string? cidade, string? complemento = null) => new(StringComparer.Ordinal)
    {
        ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
        {
            cep,
            complemento,
            cidade = cidade is null ? null : new { codigoIbge = "5002704", nome = cidade, uf = "MS" },
        }),
    };

    private static readonly Guid VersaoRetificada = Guid.CreateVersion7();

    [Fact(DisplayName = "O rascunho que adota a versão nova perde, sem recusa, a resposta, o grupo e o aceite que ela não admite, e a revisão sobe")]
    public void AdotarVersao_RetiraORespostaOGrupoEOAceiteQueAVersaoNaoAdmite()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(
            null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaDeGrupoInscricao("m1", Respostas(("PARENTESCO", "MAE"), ("RG_NUMERO", "7654321")))],
                ["FONTES_DE_RENDA"] = [new OcorrenciaDeGrupoInscricao("r1", Respostas(("RENDA_MENSAL", "1000")))],
            },
            [new AceiteDeTermo(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd), new AceiteDeTermo(TermoVeracidade, VersaoDoTermoVeracidade, HashDoTermoVeracidade)],
            Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        int revisao = inscricao.Revisao;

        RespostasAdmitidas daVersaoNova = Admitidas with
        {
            DoNivelSuperior = new HashSet<string>(StringComparer.Ordinal) { "NOME_SOCIAL" },
            SubitensPorGrupo = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = new HashSet<string>(StringComparer.Ordinal) { "PARENTESCO" },
            },
            Termos = new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)
            {
                [TermoLgpd] = new(VersaoDoTermoLgpd, "hash-lgpd-v3"),
                [TermoVeracidade] = new(VersaoDoTermoVeracidade, HashDoTermoVeracidade),
            },
        };

        Result resultado = inscricao.AdotarVersao(VersaoRetificada, daVersaoNova);

        resultado.IsSuccess.Should().BeTrue();
        inscricao.VersaoFormularioId.Should().Be(VersaoRetificada);
        inscricao.Respostas.Keys.Should().Equal("NOME_SOCIAL");
        inscricao.Grupos.Keys.Should().Equal("COMPOSICAO_FAMILIAR");
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Keys.Should().Equal("PARENTESCO");
        inscricao.Aceites.Select(static a => a.Codigo).Should().Equal(TermoVeracidade);
        inscricao.Revisao.Should().Be(revisao + 1);
    }

    [Fact(DisplayName = "O rascunho que adota a versão de uma opção de curso só perde a 2ª opção, e o que sai da oferta leva a 2ª junto")]
    public void AdotarVersao_RetiraAOfertaEA2aOpcaoQueAVersaoNaoAdmite()
    {
        Inscricao deUmaOpcao = NovaInscricao();
        SalvarOpcoes(deUmaOpcao, OfertaDoProcesso, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();
        Inscricao semAPrimeira = NovaInscricao();
        SalvarOpcoes(semAPrimeira, OfertaDoProcesso, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();

        deUmaOpcao.AdotarVersao(VersaoRetificada, Admitidas).IsSuccess.Should().BeTrue();
        semAPrimeira.AdotarVersao(VersaoRetificada, AdmitidasComDuasOpcoes with { OfertasDeCurso = new HashSet<Guid> { SegundaOfertaDoProcesso } }).IsSuccess.Should().BeTrue();

        deUmaOpcao.OfertaCursoId.Should().Be(OfertaDoProcesso);
        deUmaOpcao.OfertaCursoSegundaOpcaoId.Should().BeNull();
        semAPrimeira.OfertaCursoId.Should().BeNull();
        semAPrimeira.OfertaCursoSegundaOpcaoId.Should().BeNull();
    }

    [Fact(DisplayName = "Adotar a versão em que o rascunho já está não muda nada, nem a revisão")]
    public void AdotarVersao_NaMesmaVersao_NaoMudaNada()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.AdotarVersao(VersaoFormulario, Admitidas with { DoNivelSuperior = new HashSet<string>(StringComparer.Ordinal) });

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "A inscrição enviada não troca de versão: a adoção é recusada e a versão congelada fica")]
    public void AdotarVersao_NaInscricaoEnviada_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        SubmeterCom(inscricao, FormularioDeTeste.AvaliacaoSemOcultos).IsSuccess.Should().BeTrue();

        Result resultado = inscricao.AdotarVersao(VersaoRetificada, Admitidas);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        inscricao.VersaoFormularioId.Should().Be(VersaoFormulario);
    }

    [Fact(DisplayName = "O ETag muda com a versão do formulário, para o If-Match de uma versão não valer na adoção de outra")]
    public void ETag_DependeDaVersaoDoFormulario()
    {
        Inscricao deUma = NovaInscricao();
        Inscricao deOutra = NovaInscricao();
        deOutra.AdotarVersao(VersaoRetificada, Admitidas).IsSuccess.Should().BeTrue();
        deUma.AdotarVersao(Guid.CreateVersion7(), Admitidas).IsSuccess.Should().BeTrue();

        deUma.Revisao.Should().Be(deOutra.Revisao);
        deUma.ETag.Should().NotBe(deOutra.ETag.Replace(deOutra.Id.ToString(), deUma.Id.ToString(), StringComparison.Ordinal));
    }

    private static Inscricao NovaInscricao() =>
        Inscricao.Iniciar(Processo, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", VersaoFormulario).Value!;

    private static DateTimeOffset Instante() => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, JsonElement> Respostas() => new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
    };

    private static Dictionary<string, JsonElement> Respostas(params (string Codigo, string Valor)[] itens) =>
        itens.ToDictionary(static i => i.Codigo, static i => JsonSerializer.SerializeToElement(i.Valor), StringComparer.Ordinal);

    private static Dictionary<string, JsonElement> ComTelefone(string telefone) => new(StringComparer.Ordinal)
    {
        ["TELEFONE"] = JsonSerializer.SerializeToElement(telefone),
    };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    private static List<AceiteDeTermo> SemAceites() => [];

    private static Dictionary<string, JsonElement> SemDerivados() => new(StringComparer.Ordinal);
}
