namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitores de cadastro que dão por vivo qualquer código consultado — o pano de
/// fundo dos testes cujo assunto não é a existência da referência.
/// </summary>
/// <remarks>
/// O gate de conformidade legal recusa publicar sob regra que referencia cadastro
/// inexistente. Sem estes dublês, todo teste de publicação passaria a falhar por um
/// motivo alheio ao que ele investiga; o teste que investiga a referência órfã
/// configura o leitor por conta própria.
/// </remarks>
internal static class CadastrosVivos
{
    public static IModalidadeReader Modalidades()
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Modalidade(call.Arg<string>()));
        return reader;
    }

    public static ITipoDocumentoReader TiposDocumento()
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => TipoDocumento(call.Arg<string>()));
        return reader;
    }

    public static ITipoEtapaReader TiposEtapa()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => TipoEtapa(call.Arg<string>()));
        return reader;
    }

    public static ModalidadeView Modalidade(string codigo) =>
        new(Guid.CreateVersion7(), codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR", null, null, null, null, null, [], null, null);

    public static TipoDocumentoView TipoDocumento(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Documento", "OUTROS");

    public static TipoEtapaView TipoEtapa(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Etapa", null);
}
