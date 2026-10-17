namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;

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
    /// Reconfere os itens e os grupos e, sem nenhuma recusa, manda o agregado recopiar do catálogo
    /// os itens que podem ser recopiados; com recusa, o agregado não muda. A
    /// configuração com fato não coletável fica para a conferência de coletabilidade, que a recusa
    /// adiante. <paramref name="fatosJaColetados"/> traz, com <see cref="StringComparer.Ordinal"/>,
    /// os fatos que alguma versão publicada coletou; nulo, nenhum. Toda recusa de todo item sai no
    /// mesmo lote (ADR-0125).
    /// </summary>
    public static Result Aplicar(
        ProcessoSeletivo processo,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        IReadOnlySet<string>? fatosJaColetados)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogoDeFatos);
        // O fato que deixou de ser coletável (ou saiu do catálogo) é recusado pela conferência de
        // coletabilidade, com a causa certa; recopiar ou recusar o campo dele antes só trocaria a causa.
        if (ConferenciaDeColetabilidadeDeFatos.Conferir(processo, contexto.Fatos).IsFailure)
        {
            return Result.Success();
        }

        IReadOnlySet<string> jaColetados = fatosJaColetados ?? Nenhum;
        Dictionary<string, CopiaDoItemNoCatalogo> copias = catalogoDeFatos.ToDictionary(
            static f => f.Codigo,
            static f => new CopiaDoItemNoCatalogo(VocabularioDeFatos.OrigemValores(f), f.Formato, f.ClassificacaoProtecao),
            StringComparer.Ordinal);
        // A forma das cópias é conferida pelo agregado sem mudá-lo; o caminho e a orientação de cada
        // recusa são montados aqui, no mesmo percurso das demais.
        ILookup<FatoColetado, FieldError> daForma = processo.ConferirCopiasDoCatalogo(copias, jaColetados)
            .ToLookup<RecusaDaCopiaDoItem, FatoColetado, FieldError>(static r => r.Item, static r => r.Erro, (IEqualityComparer<FatoColetado>)ReferenceEqualityComparer.Instance);

        List<FieldError> erros = [];
        foreach (FinalidadeFormulario finalidade in processo.Campos.Select(static c => c.Finalidade).Distinct().Order())
        {
            string formulario = $"formularios[{EstruturaFormulario.ParaToken(finalidade)}]";
            FatoColetado[] itens = [.. processo.FatosColetados.Where(i => i.Finalidade == finalidade).OrderBy(static i => i.Ordem)];
            for (int indice = 0; indice < itens.Length; indice++)
            {
                erros.AddRange(Conferir(itens[indice], $"{formulario}.itens[{indice}]", contexto, jaColetados, daForma));
            }

            GrupoColetado[] grupos = [.. processo.GruposColetados.Where(g => g.Finalidade == finalidade).OrderBy(static g => g.Ordem)];
            for (int g = 0; g < grupos.Length; g++)
            {
                string caminhoDoGrupo = $"{formulario}.grupos[{g}]";
                // O grupo cujos campos já foram todos coletados está congelado como eles.
                if (!grupos[g].Subitens.All(s => jaColetados.Contains(s.FatoCodigo)))
                {
                    erros.AddRange(ConferenciaDoItemNoCatalogo.DoGrupo(grupos[g].Exibicao, grupos[g].Obrigatoriedade, contexto).Select(erro => Decorar(
                        erro, caminhoDoGrupo, $"Grupo '{grupos[g].Rotulo}' ('{grupos[g].Codigo}')", OrientacaoDoGrupo)));
                }

                FatoColetado[] campos = [.. grupos[g].Subitens.OrderBy(static s => s.Ordem)];
                for (int c = 0; c < campos.Length; c++)
                {
                    erros.AddRange(Conferir(campos[c], $"{caminhoDoGrupo}.subitens[{c}]", contexto, jaColetados, daForma));
                }
            }
        }

        // O agregado só muda com zero recusas: a recópia vem depois de toda conferência.
        return erros.Count == 0 ? processo.RecopiarItensDoCatalogo(copias, jaColetados) : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Roda a operação e, se ela recusa, descarta o que a recópia mudou no agregado rastreado: a
    /// recusa não devolve exceção, e o Wolverine salvaria o que sobrou (ADR-0119).
    /// </summary>
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> SemPersistirEmRecusaAsync(
        ISelecaoUnitOfWork unitOfWork,
        Func<Task<(Result Resposta, IEnumerable<object> Eventos)>> operacao)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(operacao);

        (Result Resposta, IEnumerable<object> Eventos) resultado = await operacao().ConfigureAwait(false);
        if (resultado.Resposta.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return resultado;
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

    private static FieldError Decorar(FieldError erro, string caminho, string quem, string orientacao) => new(
        string.IsNullOrEmpty(erro.Field) ? caminho : $"{caminho}.{erro.Field}",
        new DomainError(erro.Error.Code, $"{erro.Error.Message} {quem}. {orientacao}"));
}
