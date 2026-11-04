namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Interfaces;

/// <summary>
/// Os cadastros de onde se lê a cópia selada privada de cada documento divulgado e o acervo público para
/// onde ela é copiada, com o resolvedor do endereço divulgado.
/// </summary>
internal sealed record AcervoDaDivulgacao(
    IDocumentoAPublicarRepository Documentos,
    IModeloDeDocumentoRepository Modelos,
    IAcervoPublico Acervo,
    IEnderecoNoAcervoPublico Endereco);
