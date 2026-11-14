namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AtoDoProcessoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);

    private static NovoAtoDoProcesso Novo(
        Guid? atoCorrigidoId = null,
        string? motivo = null,
        Guid? principal = null,
        IReadOnlyList<AnexoDeclarado>? anexos = null,
        string? politicaConfirmadaPor = null) =>
        new(
            AtoId: Guid.CreateVersion7(),
            ProcessoSeletivoId: Guid.CreateVersion7(),
            TipoCodigo: "AVISO",
            TipoNome: "Aviso",
            Numero: "3/2026",
            DataDoAto: new DateOnly(2026, 12, 10),
            Assinante: "Diretor do CEPS",
            UnidadeAdministradora: "ceps",
            IdentificadorLegivelDoProcesso: "psm-medicina-2027",
            DocumentoPrincipalId: principal ?? Guid.CreateVersion7(),
            Anexos: anexos ?? [],
            AtoCorrigidoId: atoCorrigidoId,
            MotivoDaCorrecao: motivo,
            PoliticaDeDivulgacaoConfirmadaPorSub: politicaConfirmadaPor);

    [Fact(DisplayName = "O registro nasce pendente, com os anexos na ordem declarada e a confirmação da política datada e atribuída")]
    public void Registrar_NascePendenteComAnexosNaOrdemDeclarada()
    {
        Guid segundo = Guid.CreateVersion7();
        Guid primeiro = Guid.CreateVersion7();
        NovoAtoDoProcesso novo = Novo(
            anexos: [new AnexoDeclarado(segundo, "Anexo B"), new AnexoDeclarado(primeiro, "Anexo A")],
            politicaConfirmadaPor: "sub-do-usuario");

        AtoDoProcesso ato = AtoDoProcesso.Registrar(novo, Agora).Value!;

        ato.Id.Should().Be(novo.AtoId, "o registro é identificado pelo ato que a Seleção decidiu");
        ato.PoliticaDeDivulgacaoConfirmadaEm.Should().Be(Agora);
        ato.PoliticaDeDivulgacaoConfirmadaPorSub.Should().Be("sub-do-usuario");
        ato.Anexos.Select(a => (a.Ordem, a.DocumentoAPublicarId, a.NomeDeExibicao))
            .Should().Equal((0, segundo, "Anexo B"), (1, primeiro, "Anexo A"));
    }

    [Fact(DisplayName = "O registro guarda o instante da divulgação que recebe, sem confundi-lo com a data que o documento declara")]
    public void Registrar_GuardaOInstanteDaPublicacaoRecebido()
    {
        DateTimeOffset publicadoEm = new(2026, 12, 11, 2, 30, 0, TimeSpan.Zero);

        AtoDoProcesso ato = AtoDoProcesso.Registrar(Novo(), publicadoEm).Value!;

        ato.PublicadoEm.Should().Be(publicadoEm);
        ato.DataDoAto.Should().Be(new DateOnly(2026, 12, 10));
    }

    [Fact(DisplayName = "O registro sem instante de publicação é violação de programação, não recusa de negócio")]
    public void Registrar_SemInstanteDaPublicacao_Lanca()
    {
        Func<Result<AtoDoProcesso>> registrar = () => AtoDoProcesso.Registrar(Novo(), default);

        registrar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Ato corrigido sem motivo é recusado com falha nomeada no campo do motivo")]
    public void Registrar_AtoCorrigidoSemMotivo_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(atoCorrigidoId: Guid.CreateVersion7()), Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "motivoDaCorrecao"
                && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
    }

    [Fact(DisplayName = "Motivo sem ato corrigido é recusado com falha nomeada no campo do ato")]
    public void Registrar_MotivoSemAtoCorrigido_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(motivo: "Erro de grafia"), Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "atoCorrigidoId"
                && e.Error.Code == AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio);
    }

    [Fact(DisplayName = "Ato corrigido com identificador vazio é recusado com falha nomeada")]
    public void Registrar_AtoCorrigidoVazio_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(atoCorrigidoId: Guid.Empty, motivo: "Erro de grafia"), Agora);

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "atoCorrigidoId"
                && e.Error.Code == AtoDoProcessoErrorCodes.AtoCorrigidoInvalido);
    }

    [Fact(DisplayName = "Um ato não corrige a si mesmo: o identificador é do servidor, então a violação é de programação")]
    public void Registrar_AtoQueCorrigeASiMesmo_Lanca()
    {
        NovoAtoDoProcesso novo = Novo(motivo: "Erro de grafia");
        Func<Result<AtoDoProcesso>> registrar = () => AtoDoProcesso.Registrar(novo with { AtoCorrigidoId = novo.AtoId }, Agora);

        registrar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Um documento não é o principal e anexo, nem anexo duas vezes")]
    public void Registrar_DocumentoRepetido_Lanca()
    {
        Guid principal = Guid.CreateVersion7();
        Guid anexo = Guid.CreateVersion7();

        Func<Result<AtoDoProcesso>> principalComoAnexo = () => AtoDoProcesso.Registrar(
            Novo(principal: principal, anexos: [new AnexoDeclarado(principal, "Anexo")]), Agora);
        Func<Result<AtoDoProcesso>> anexoDuasVezes = () => AtoDoProcesso.Registrar(
            Novo(anexos: [new AnexoDeclarado(anexo, "Anexo"), new AnexoDeclarado(anexo, "Outro")]), Agora);

        principalComoAnexo.Should().Throw<ArgumentException>();
        anexoDuasVezes.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Texto opcional em branco é ausência: vira nulo, sem exceção")]
    public void Registrar_TextoOpcionalEmBranco_ViraNulo()
    {
        NovoAtoDoProcesso novo = Novo(politicaConfirmadaPor: "  ") with { Numero = "   " };

        AtoDoProcesso ato = AtoDoProcesso.Registrar(novo, Agora).Value!;

        ato.Numero.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaPorSub.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaEm.Should().BeNull();
        ato.MotivoDaCorrecao.Should().BeNull();
    }
}
