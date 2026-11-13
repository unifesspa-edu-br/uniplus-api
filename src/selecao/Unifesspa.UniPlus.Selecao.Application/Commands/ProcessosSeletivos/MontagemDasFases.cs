namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta as fases do cronograma de uma edição a partir do payload (ver <see cref="FasesEmMontagem"/>). Guarda só as
/// portas dos cadastros vivos que a montagem consulta; o que a edição acumula, e a leitura memoizada dos cadastros,
/// nascem a cada montagem, e por isso o serviço não tem estado entre elas.
/// </summary>
public sealed class MontagemDasFases(
    IFaseCanonicaReader faseCanonicaReader,
    IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
    ITipoBancaReader tipoBancaReader,
    ICategoriaDocumentoReader categoriaDocumentoReader,
    IRegraCatalogoReader regraCatalogoReader)
{
    internal Task<Result<List<FaseCronograma>>> MontarAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<FaseCronogramaInput> fases,
        AtosConsultadosNaEdicao atos,
        CancellationToken cancellationToken)
    {
        CadastrosDasFases cadastros = new(
            faseCanonicaReader, unidadeResponsavelResolvedor, tipoBancaReader, categoriaDocumentoReader, regraCatalogoReader);
        return new FasesEmMontagem(processo, cadastros, atos).MontarAsync(fases, cancellationToken);
    }
}
