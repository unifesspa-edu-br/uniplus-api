namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Cadastros de Configuração como os testes de outro assunto precisam vê-los: contendo os
/// códigos que as regras e os processos destes cenários referenciam.
/// </summary>
/// <remarks>
/// <para>O gate de conformidade legal recusa publicar sob regra que referencia cadastro
/// inexistente. Sem estes dublês, todo teste de publicação passaria a falhar por um motivo
/// alheio ao que ele investiga; o teste que investiga a referência configura o leitor por
/// conta própria.</para>
/// <para>Sem argumento, cada método devolve o catálogo com os códigos que esta suíte usa —
/// listados abaixo, para que o dublê não precise adivinhar. Um cenário que dependa de outro
/// código o declara na chamada, e é o próprio teste que fica dizendo o que o cadastro tem.</para>
/// </remarks>
internal static class CadastrosVivos
{
    private static readonly string[] ModalidadesDaSuite = ["AC", "LB_PPI", "LB_Q", "LB_EP"];
    private static readonly string[] TiposDocumentoDaSuite = ["LAUDO_MEDICO"];
    private static readonly string[] TiposEtapaDaSuite = ["PROVA_OBJETIVA", "ENTREVISTA", "ETAPA_NAO_OFERTADA"];

    public static IModalidadeReader Modalidades(params string[] codigos)
    {
        string[] vivos = codigos.Length == 0 ? ModalidadesDaSuite : codigos;
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Array.Exists(vivos, c => c == call.Arg<string>().Trim())
                ? Modalidade(call.Arg<string>().Trim())
                : null);
        reader.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns(Array.ConvertAll(vivos, Modalidade));
        return reader;
    }

    public static ITipoDocumentoReader TiposDocumento(params string[] codigos)
    {
        string[] vivos = codigos.Length == 0 ? TiposDocumentoDaSuite : codigos;
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Array.Exists(vivos, c => c == call.Arg<string>().Trim())
                ? TipoDocumento(call.Arg<string>().Trim())
                : null);
        reader.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns(Array.ConvertAll(vivos, TipoDocumento));
        return reader;
    }

    public static ITipoEtapaReader TiposEtapa(params string[] codigos)
    {
        string[] vivos = codigos.Length == 0 ? TiposEtapaDaSuite : codigos;
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Array.Exists(vivos, c => c == call.Arg<string>().Trim())
                ? TipoEtapa(call.Arg<string>().Trim())
                : null);
        reader.ListarAtivosAsync(Arg.Any<CancellationToken>())
            .Returns(Array.ConvertAll(vivos, TipoEtapa));
        return reader;
    }

    public static ModalidadeView Modalidade(string codigo) =>
        new(Guid.CreateVersion7(), codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR", null, null, null, null, null, [], null, null);

    public static TipoDocumentoView TipoDocumento(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Documento", "OUTROS");

    public static TipoEtapaView TipoEtapa(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Etapa", null);
}
