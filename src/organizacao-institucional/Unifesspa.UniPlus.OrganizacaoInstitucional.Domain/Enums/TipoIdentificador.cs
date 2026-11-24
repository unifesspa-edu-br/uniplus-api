namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Tipo do identificador cujo histórico está sendo registrado em
/// <c>UnidadeIdentificadorHistorico</c>. O identificador no endereço web, a sigla e o código são únicos entre
/// Unidades vivas; o apelido não é único. O código de cada membro é o valor gravado na coluna <c>tipo</c>.
/// </summary>
[DescricaoDoContrato("O tipo de identificador de uma unidade cujo histórico é guardado: identificador no endereço web, sigla, código ou apelido.")]
public enum TipoIdentificador
{
    [CodigoDeContrato("IDENTIFICADOR_NO_ENDERECO_WEB", "Identificador no endereço web", Descricao = "O texto que identifica a unidade no endereço de página da web; é único entre as unidades ativas.")]
    IdentificadorNoEnderecoWeb = 1,
    [CodigoDeContrato("SIGLA", "Sigla", Descricao = "A sigla da unidade; é única entre as unidades ativas.")]
    Sigla = 2,
    [CodigoDeContrato("CODIGO", "Código", Descricao = "O código da unidade; é único entre as unidades ativas.")]
    Codigo = 3,
    [CodigoDeContrato("APELIDO", "Apelido", Descricao = "O nome pelo qual a unidade também é conhecida; pode se repetir entre unidades.")]
    Apelido = 4,
}
