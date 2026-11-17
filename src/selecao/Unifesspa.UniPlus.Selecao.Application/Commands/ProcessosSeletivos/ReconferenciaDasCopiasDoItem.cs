namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// Reconfere, ao congelar, o item do formulário contra o catálogo de hoje: o item é editável depois
/// de gravado, e congelar a cópia velha publicaria o campo com o desenho antigo e gravaria sem
/// cifra o dado que passou a ser identificador. O fato que nenhuma versão publicada coletou passa
/// pela mesma conferência da gravação do item e tem as cópias do catálogo refeitas pelo agregado;
/// o já coletado só tem o campo conferido. Roda antes dos gates de conformidade, para que eles
/// avaliem a cópia nova.
/// </summary>
/// <remarks>
/// O campo do fato já coletado tem de seguir coerente com o fato do catálogo, ou a retificação
/// republicaria o campo incoerente; as regras dele não são reconferidas, porque os valores de
/// domínio nunca saem do catálogo.
/// </remarks>
internal static class ReconferenciaDasCopiasDoItem
{
    private const string OrientacaoDoJaColetado =
        "O fato já foi coletado por uma versão publicada e o campo dele não pode mudar: desfaça a correção no catálogo " +
        "enquanto o processo precisar ser retificado (a definição corrigida vale para processos novos e rascunhos).";

    private const string OrientacaoDoRascunho = "Regrave o item no formulário do processo.";

    private const string OrientacaoDoGrupo = "Regrave o grupo no formulário do processo.";

    private static readonly HashSet<string> Nenhum = new(StringComparer.Ordinal);

    /// <summary>
    /// Reconfere os itens e os grupos sem mudar o agregado e devolve as cópias do catálogo que ele deve
    /// refazer. A configuração com fato não coletável fica para a conferência de coletabilidade, que a
    /// recusa adiante. <paramref name="fatosJaColetados"/> traz, com <see cref="StringComparer.Ordinal"/>,
    /// os fatos que alguma versão publicada coletou; nulo, nenhum. Toda recusa de todo item sai no mesmo
    /// lote (ADR-0125).
    /// </summary>
    public static Result<CopiasConferidas> Conferir(
        ProcessoSeletivo processo, IReadOnlyList<FatoCandidatoView> catalogoDeFatos, IReadOnlySet<string>? fatosJaColetados)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogoDeFatos);

        return FatoNaoColetavel(processo, contexto)
            ? Result<CopiasConferidas>.Success(new CopiasConferidas(new Dictionary<string, CopiaDoItemNoCatalogo>(), Nenhum))
            : ConferirAsCopias(processo, catalogoDeFatos, contexto, fatosJaColetados ?? Nenhum);
    }

    // O fato que deixou de ser coletável (ou saiu do catálogo) é recusado pela conferência de coletabilidade, com a causa
    // certa; recopiar ou recusar o campo dele antes só trocaria a causa.
    private static bool FatoNaoColetavel(ProcessoSeletivo processo, ContextoDoCatalogo contexto) =>
        ConferenciaDeColetabilidadeDeFatos.Conferir(processo, contexto.Fatos).IsFailure;

    private static Result<CopiasConferidas> ConferirAsCopias(
        ProcessoSeletivo processo, IReadOnlyList<FatoCandidatoView> catalogoDeFatos, ContextoDoCatalogo contexto, IReadOnlySet<string> jaColetados)
    {
        Dictionary<string, CopiaDoItemNoCatalogo> copias = CopiasDoCatalogo(catalogoDeFatos);
        ILookup<FatoColetado, FieldError> daForma = RecusasDaFormaPorItem(processo, copias, jaColetados);
        List<FieldError> erros = [.. RecusasDoDocumento(processo, contexto, jaColetados, daForma)];

        return erros.Count == 0
            ? Result<CopiasConferidas>.Success(new CopiasConferidas(copias, jaColetados))
            : Result<CopiasConferidas>.ValidationFailure(erros);
    }

    private static Dictionary<string, CopiaDoItemNoCatalogo> CopiasDoCatalogo(IReadOnlyList<FatoCandidatoView> catalogoDeFatos) =>
        catalogoDeFatos.ToDictionary(
            static f => f.Codigo,
            static f => new CopiaDoItemNoCatalogo(
                VocabularioDeFatos.OrigemValores(f), f.Formato, f.ClassificacaoProtecao, f.HipoteseLegal, f.FinalidadeTratamento,
                f.Descricao, f.ComponenteExibicao),
            StringComparer.Ordinal);

    // A forma das cópias é conferida pelo agregado sem mudá-lo; o caminho e a orientação de cada
    // recusa são montados aqui, no mesmo percurso das demais.
    private static ILookup<FatoColetado, FieldError> RecusasDaFormaPorItem(
        ProcessoSeletivo processo, Dictionary<string, CopiaDoItemNoCatalogo> copias, IReadOnlySet<string> jaColetados) =>
        RegrasDaColeta.ConferirCopiasDoCatalogo(processo.Declaracao(), copias, jaColetados)
            .ToLookup<RecusaDaCopiaDoItem, FatoColetado, FieldError>(static r => r.Item, static r => r.Erro, (IEqualityComparer<FatoColetado>)ReferenceEqualityComparer.Instance);

    private static IEnumerable<FieldError> RecusasDoDocumento(
        ProcessoSeletivo processo, ContextoDoCatalogo contexto, IReadOnlySet<string> jaColetados, ILookup<FatoColetado, FieldError> daForma) =>
        ItensDoFormularioNoDocumento.De(processo).SelectMany(formulario =>
            formulario.Itens.SelectMany(i => Conferir(i.Item, i.Caminho, contexto, jaColetados, daForma))
                .Concat(formulario.Grupos.SelectMany(g => ConferirGrupo(g, contexto, jaColetados, daForma))));

    private static IEnumerable<FieldError> ConferirGrupo(
        GrupoNoDocumento grupo, ContextoDoCatalogo contexto, IReadOnlySet<string> jaColetados, ILookup<FatoColetado, FieldError> daForma)
    {
        // O grupo cujos campos já foram todos coletados está congelado como eles.
        IEnumerable<FieldError> doGrupo = grupo.Campos.All(c => jaColetados.Contains(c.Item.FatoCodigo))
            ? []
            : ConferenciaDoItemNoCatalogo.DoGrupo(grupo.Grupo.Exibicao, grupo.Grupo.Obrigatoriedade, contexto).Select(erro => Decorar(
                erro, grupo.Caminho, $"Grupo '{grupo.Grupo.Rotulo}' ('{grupo.Grupo.Codigo}')", OrientacaoDoGrupo));

        return [.. doGrupo, .. grupo.Campos.SelectMany(c => Conferir(c.Item, c.Caminho, contexto, jaColetados, daForma))];
    }

    private static IEnumerable<FieldError> Conferir(
        FatoColetado item, string caminho, ContextoDoCatalogo contexto, IReadOnlySet<string> jaColetados, ILookup<FatoColetado, FieldError> daForma)
    {
        if (!contexto.Fatos.TryGetValue(item.FatoCodigo, out FatoCandidatoView? fato))
        {
            return [];
        }

        bool jaColetado = jaColetados.Contains(item.FatoCodigo);
        string orientacao = jaColetado ? OrientacaoDoJaColetado : OrientacaoDoRascunho;

        // A causa real da recusa (domínio, cardinalidade, fonte, restrição, formato…) segue como
        // veio; só o item e a orientação se somam a ela.
        return ConferenciaDoItemNoCatalogo.DoItemGravado(item, fato, jaColetado, contexto).Concat(daForma[item]).Select(erro => Decorar(
            erro, caminho, $"Item '{item.Rotulo}' (fato '{item.FatoCodigo}')", orientacao));
    }

    private static FieldError Decorar(FieldError erro, string caminho, string quem, string orientacao) =>
        new(erro.NoCaminhoDe(caminho).Field, new DomainError(erro.Error.Code, $"{erro.Error.Message} {quem}. {orientacao}"));
}

/// <summary>As cópias do catálogo conferidas e os fatos já coletados que o agregado usa para refazê-las.</summary>
internal sealed record CopiasConferidas(
    IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> Copias,
    IReadOnlySet<string> FatosJaColetados);
