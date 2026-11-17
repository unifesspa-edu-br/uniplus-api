namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// O que o modelo já tem gravado e a edição toma como referência: o conteúdo, de onde o item básico
/// omitido volta como está, o tipo de processo, que só é conferido quando muda, e os vínculos a
/// catálogo, que só são recusados quando novos. A criação não tem referência (<see cref="Nenhum"/>).
/// </summary>
internal sealed record ModeloGravado(ConteudoDoModeloInput? Conteudo, string? TipoProcessoCodigo, VinculosDoModelo Vinculos)
{
    public static ModeloGravado Nenhum { get; } = new(null, null, VinculosDoModelo.Nenhum);

    public static ModeloGravado De(ModeloFormulario modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return new(modelo.ToView().Conteudo, modelo.TipoProcessoCodigo, ConferenciaDoModelo.VinculosGravados(modelo.Conteudo));
    }
}
