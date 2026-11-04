namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Interfaces;

/// <summary>
/// Os cadastros de onde se lê a cópia selada privada de cada documento e o acervo público para onde
/// se copia, com o resolvedor do endereço divulgado.
/// </summary>
/// <param name="Documentos">Os documentos a publicar, com a cópia selada privada.</param>
/// <param name="Modelos">Os modelos de documento, com a cópia selada privada.</param>
/// <param name="Acervo">O acervo público, destino da cópia.</param>
/// <param name="Endereco">O resolvedor do endereço público de uma chave do acervo.</param>
internal sealed record CopiaParaOAcervo(
    IDocumentoAPublicarRepository Documentos,
    IModeloDeDocumentoRepository Modelos,
    IAcervoPublico Acervo,
    IEnderecoNoAcervoPublico Endereco);
