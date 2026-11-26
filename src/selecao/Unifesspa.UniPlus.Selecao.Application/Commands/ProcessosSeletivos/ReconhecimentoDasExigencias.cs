namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Chaves;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

internal sealed class ReconhecimentoDasExigencias
{
    private readonly Dictionary<string, DocumentoExigido> _reconhecidas;
    private readonly ChavesEmUso _chaves = new();

    internal ReconhecimentoDasExigencias(
        IEnumerable<DocumentoExigido> existentes,
        IReadOnlyList<RegraDeComprovacao> mantidas,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas)
    {
        foreach (DocumentoExigido documento in mantidas.SelectMany(static regra => regra.EnumerarRegraEItens())
            .Select(static regra => regra.DocumentoExigido).OfType<DocumentoExigido>())
        {
            _chaves.Adicionar(documento.Chave);
        }

        HashSet<string> solicitadas = [.. entradas.Select(static regra => regra.Documento?.Chave).OfType<string>()];
        _reconhecidas = existentes.Where(documento => solicitadas.Contains(documento.Chave.Valor) && !_chaves.Contem(documento.Chave))
            .ToDictionary(static documento => documento.Chave.Valor, StringComparer.Ordinal);
        foreach (DocumentoExigido documento in _reconhecidas.Values)
        {
            _chaves.Adicionar(documento.Chave);
        }
    }

    internal Result<DocumentoExigido> Criar(string? chaveInformada, string codigo, DocumentoExigidoDeclarado declarado)
    {
        DocumentoExigido? existente = null;
        if (chaveInformada is not null)
        {
            _reconhecidas.Remove(chaveInformada, out existente);
        }
        ChaveLocal chave = existente?.Chave ?? FormacaoDeChaveLocal.Formar("exigencia", codigo, _chaves);
        _chaves.Adicionar(chave);
        Result<DocumentoExigido> criado = DocumentoExigido.Criar(chave, declarado);
        if (criado.IsFailure || existente is null)
        {
            return criado;
        }
        IndeferimentoDoDocumento indeferimento = declarado.Indeferimento with { Consequencia = criado.Value!.ConsequenciaIndeferimento?.Codigo() };
        return Result<DocumentoExigido>.Success(DocumentoExigido.Reidratar(chave, existente.Id, declarado with { Indeferimento = indeferimento }));
    }
}
