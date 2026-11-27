namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A declaração do ato conferida de uma vez: todas as recusas juntas, na ordem do corpo do pedido.</summary>
public sealed class ConferenciaDaDeclaracaoDoAtoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static readonly DeclaracaoDoAtoNormativo Valida = new()
    {
        Numeracao = new NumeracaoDoAto("CEPS", "EDITAL", 2026, "13"),
        TipoCodigo = "EDITAL_ABERTURA",
        DocumentoHash = HashValido,
        Assinante = PersonasDeTeste.NomeKevinCarvalho,
        VersaoDoObjetoId = null,
        VersaoDoObjetoHash = null,
        Retificacao = default,
        Vinculos = [new EntidadeVinculada("PROCESSO_SELETIVO", ProcessoId)],
        Anexos = [("Anexo I.pdf", "application/pdf", HashValido)],
    };

    private static readonly DeclaracaoDoAtoNormativo TudoInvalido = new()
    {
        Numeracao = new NumeracaoDoAto("", "", 0, new string('n', 61)),
        TipoCodigo = "edital",
        DocumentoHash = "abc",
        Assinante = null,
        VersaoDoObjetoId = Guid.CreateVersion7(),
        VersaoDoObjetoHash = null,
        Retificacao = new RelacaoDeRetificacao(null, new string('m', 1001)),
        Vinculos = [new EntidadeVinculada("", Guid.Empty), new EntidadeVinculada("PROCESSO", ProcessoId), null],
        Anexos = [.. Enumerable.Range(0, AnexoDoAto.QuantidadeMaxima).Select(i => ((string?, string?, string?)?)($"A{i}.pdf", "application/pdf", HashValido)), null],
    };

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(DeclaracaoDoAtoNormativo declaracao) =>
        ResumoDasRecusas.De(ConferenciaDaDeclaracaoDoAto.De(declaracao).Recusas);

    [Fact(DisplayName = "Declaração válida não tem recusa e cada parte traz o valor conferido")]
    public void De_Valida_SemRecusaECadaPartePreenchida()
    {
        ConferenciaDaDeclaracaoDoAto conferencia = ConferenciaDaDeclaracaoDoAto.De(Valida);

        conferencia.Recusas.Should().BeEmpty();
        conferencia.Numeracao.Valor.Should().Be(Valida.Numeracao);
        conferencia.CodigoDoTipo.Valor.Should().Be("EDITAL_ABERTURA");
        conferencia.Documento.Valor.Should().Be((HashValido, PersonasDeTeste.NomeKevinCarvalho));
        conferencia.VersaoDoObjeto.Valor.Should().BeNull();
        conferencia.Vinculos.Should().ContainSingle().Which.Valor.Should().Be(Valida.Vinculos[0]);
    }

    [Fact(DisplayName = "Órgão em branco, hash inválido e série longa devolvem as três recusas, a série antes do hash")]
    public void De_OrgaoHashESerie_DevolveAsTresNaOrdemDoCorpo()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            Numeracao = Valida.Numeracao with { Orgao = " ", Serie = new string('s', 101) },
            DocumentoHash = "abc",
        };

        Recusas(declaracao).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("serie", AtoNormativoErrorCodes.TextoInvalido),
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido));
    }

    [Fact(DisplayName = "Órgão em branco e vínculo com tipo fora da forma são recusados na mesma conferência")]
    public void De_OrgaoEVinculoMalformado_RecusaOsDois()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            Numeracao = Valida.Numeracao with { Orgao = "" },
            Vinculos = [new EntidadeVinculada("processo", ProcessoId)],
        };

        Recusas(declaracao).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido));
    }

    [Fact(DisplayName = "Ato retificado sem motivo e hash inválido devolvem o hash e a retificação, nessa ordem")]
    public void De_RetificacaoSemMotivoEHashInvalido_DevolveHashERetificacao()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            DocumentoHash = "abc",
            Retificacao = new RelacaoDeRetificacao(Guid.CreateVersion7(), null),
        };

        Recusas(declaracao).Should().Equal(
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido),
            ("motivoRetificacao", AtoNormativoErrorCodes.RetificacaoIncompleta));
    }

    [Fact(DisplayName = "Ato retificado vazio com motivo e hash inválido devolvem o hash e uma recusa só da retificação")]
    public void De_AtoRetificadoVazioEHashInvalido_DevolveHashEAtoRetificado()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            DocumentoHash = "abc",
            Retificacao = new RelacaoDeRetificacao(Guid.Empty, "corrige o anexo II"),
        };

        ConferenciaDaDeclaracaoDoAto conferencia = ConferenciaDaDeclaracaoDoAto.De(declaracao);

        ResumoDasRecusas.De(conferencia.Recusas).Should().Equal(
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido),
            ("atoRetificadoId", AtoNormativoErrorCodes.RetificacaoIncompleta));
        conferencia.Retificacao.EstaAceita.Should().BeFalse();
    }

    [Fact(DisplayName = "Vínculo nulo é recusado na posição, e os demais vínculos continuam conferidos")]
    public void De_VinculoNulo_RecusaNaPosicao()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            Vinculos = [null, new EntidadeVinculada("PROCESSO", Guid.Empty)],
        };

        Recusas(declaracao).Should().Equal(
            ("vinculos[0]", AtoNormativoErrorCodes.VinculoNulo),
            ("vinculos[1].entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade));
    }

    [Fact(DisplayName = "Anexos com hash inválido e com nome repetido são recusados, cada um na sua posição")]
    public void De_AnexosComProblemasDiferentes_RecusaCadaUm()
    {
        DeclaracaoDoAtoNormativo declaracao = Valida with
        {
            Anexos =
            [
                ("A.pdf", "application/pdf", "abc"),
                ("B.pdf", "application/pdf", HashValido),
                ("B.pdf", "application/pdf", HashValido),
            ],
        };

        Recusas(declaracao).Should().Equal(
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido),
            ("anexos[2].nome", AnexoDoAtoErrorCodes.NomeDuplicado));
    }

    [Fact(DisplayName = "Todas as famílias recusadas saem na ordem do corpo do pedido")]
    public void De_TudoInvalido_SaiNaOrdemDoCorpo()
    {
        Recusas(TudoInvalido).Select(recusa => recusa.Campo).Should().Equal(
            "orgao", "serie", "ano", "numero", "tipoCodigo", "documentoHash", "assinante", "versaoDoObjetoHash",
            "atoRetificadoId", "motivoRetificacao", "vinculos[0].entidadeTipo", "vinculos[0].entidadeId", "vinculos[2]",
            "anexos", "anexos[50]");
    }

    [Fact(DisplayName = "Declaração nula é recusada")]
    public void De_Nula_Lanca()
    {
        Action acao = () => ConferenciaDaDeclaracaoDoAto.De(null!);

        acao.Should().Throw<ArgumentNullException>();
    }
}
