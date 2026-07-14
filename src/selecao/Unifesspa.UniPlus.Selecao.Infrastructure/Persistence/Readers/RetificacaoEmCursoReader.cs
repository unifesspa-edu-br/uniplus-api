namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class RetificacaoEmCursoReader : IRetificacaoEmCursoReader
{
    private readonly SelecaoDbContext _context;

    public RetificacaoEmCursoReader(SelecaoDbContext context) => _context = context;

    public async Task<RetificacaoEmCursoDto?> ObterAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        // Materializa a ENTIDADE e deixa o DTO se montar a partir dela — em vez de projetar
        // os campos direto no SQL. O motivo é o ETag: projetá-lo no `Select` obrigaria a
        // remontar o formato `"{Id}:{Revisao}"` aqui, e um dia alguém mudaria o formato no
        // domínio e não aqui. A sessão editorial passaria a devolver, no GET, um tag que a
        // precondição não reconhece — e o administrador levaria 412 num rascunho que
        // acabou de ler. É uma linha só: não há o que otimizar contra esse risco.
        RascunhoRetificacao? rascunho = await _context.RascunhosRetificacao
            .AsNoTracking()
            .Where(r => r.ProcessoSeletivoId == processoSeletivoId
                // O EXISTS através de ProcessosSeletivos herda o filtro global de
                // soft-delete: um processo excluído logicamente não vaza a sua sessão
                // editorial — cai no mesmo 404 que o resto da API.
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return rascunho is null ? null : RetificacaoEmCursoDto.De(rascunho);
    }
}
