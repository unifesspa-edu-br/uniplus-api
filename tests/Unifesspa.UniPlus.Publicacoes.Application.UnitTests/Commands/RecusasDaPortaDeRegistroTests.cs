namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A porta de escrita devolve todas as causas de uma declaração malformada, na ordem do pedido e sem o campo
/// do corpo do endpoint, que não existe no pedido de quem publica por ela.
/// </summary>
public sealed class RecusasDaPortaDeRegistroTests
{
    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "Órgão em branco e hash inválido são devolvidos juntos, sem campo e sem adicionar o ato")]
    public async Task Registrar_DeclaracaoMalformada_DevolveTodasAsCausasSemCampo()
    {
        RegistroDeAtoNormativoPeloDominio porta = new(MontagemDeTeste.Criar(_tipos, _atos), _atos);

        Result resultado = await porta.RegistrarAsync(
            Ato() with { Orgao = " ", DocumentoHash = "abc" }, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        ResumoDasRecusas.De(resultado.Errors).Should().Equal(
            ((string?)null, AtoNormativoErrorCodes.TextoInvalido),
            (null, AtoNormativoErrorCodes.HashDoDocumentoInvalido));
        await _atos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    private static AtoNormativoPublicado Ato() =>
        new()
        {
            AtoId = Guid.CreateVersion7(),
            Orgao = "CEPS",
            Serie = "EDITAL",
            Ano = 2026,
            Numero = "13",
            TipoCodigo = ComandoDeRegistroDeTeste.Tipo,
            AtributosDoTipo = new(CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, "Edital de abertura"),
            DataDoAto = new DateOnly(2026, 3, 10),
            PublicadoEm = ComandoDeRegistroDeTeste.PublicadoEm,
            DocumentoHash = ComandoDeRegistroDeTeste.HashValido,
            Assinante = PersonasDeTeste.NomeKevinCarvalho,
            Vinculos = [],
        };
}
