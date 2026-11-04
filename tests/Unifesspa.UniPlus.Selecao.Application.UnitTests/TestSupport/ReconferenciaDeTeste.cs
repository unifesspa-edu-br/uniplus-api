namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A reconferência dos itens contra o catálogo seguida da recópia pelo agregado, sem unidades nem proteção.</summary>
internal static class ReconferenciaDeTeste
{
    internal static Result Aplicar(
        ProcessoSeletivo processo, IReadOnlyList<FatoCandidatoView> catalogo, IReadOnlySet<string>? fatosJaColetados)
    {
        Result<CopiasConferidas> copias = ReconferenciaDasCopiasDoItem.Conferir(processo, catalogo, fatosJaColetados);
        return copias.IsFailure
            ? Result.ValidationFailure(copias.Errors)
            : processo.PrepararCongelamento(new PreparacaoDoCongelamento(
                copias.Value!.Copias,
                copias.Value.FatosJaColetados,
                new Dictionary<Guid, UnidadeResponsavelSnapshot>(),
                new ProtecaoVigenteDasExigencias(
                    new Dictionary<string, ClassificacaoDeProtecao>(), new Dictionary<Guid, ProtecaoDoDocumento>(), new HashSet<string>())));
    }
}
