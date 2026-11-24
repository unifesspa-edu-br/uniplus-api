namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Domain.Cidades;
using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

using ValoresDoCampo = System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Selecao.Application.Abstractions.ValorDominioDeclaradoCongelado>;

/// <summary>
/// Resolve, para cada <see cref="FatoColetado"/> do processo, os valores que o candidato pode
/// escolher (UNI-REQ-0072) — a chave que falta hoje no envelope público: <c>fatosColetados[]</c>
/// carrega rótulo/tipo de renderização/obrigatoriedade, mas nada sobre QUAIS
/// valores o seletor oferece.
/// </summary>
/// <remarks>
/// <para>
/// Uma entrada por <see cref="FatoColetado"/>, sempre — não só pelos de seleção. Para
/// <c>SIM_OU_NAO</c>/<c>NUMERO</c> o valor é <see langword="null"/> (a mesma correspondência de
/// <c>SerializarFatosColetados</c>); para <c>ESCOLHA_UNICA</c>/<c>ESCOLHA_MULTIPLA</c> é a lista
/// — possivelmente vazia, nunca ausente. É este dicionário completo, e não um parcial só com os
/// fatos de seleção, que o decoder reproduz ao reidratar, então o encoder trata os dois
/// caminhos (publicação nova e retificação) pela MESMA forma.
/// </para>
/// <para>
/// Origens para o valor de um fato categórico coletado (UNI-REQ-0072, ADR-0136). O
/// catálogo de fonte GLOBAL (COR_RACA, SEXO, NACIONALIDADE — <c>ValoresDominioDeclarados</c>) é
/// filtrado: entram os valores ativos e os desativados que uma condição do processo já cita, porque
/// desativar recusa só vínculo novo (ADR-0136). Para as demais, decide a origem copiada para o
/// campo coletado (<c>ProcessoSeletivo.OrigemDasOpcoes</c>), a mesma que o agregado confere na
/// publicação: as opções do PRÓPRIO processo, pela mesma leitura que valida predicado
/// (<c>ProcessoSeletivo.OpcoesDoProcesso</c>); ou os municípios do bônus regional, com o código
/// IBGE como valor, "Município/UF" como rótulo e a ordem alfabética de nome de
/// <c>VocabularioDeFatos.MunicipiosDoBonus</c>; ou os municípios da prova presencial, na ordem declarada.
/// </para>
/// <para>
/// Não faz I/O próprio — recebe o catálogo já lido UMA vez pelo handler, o mesmo
/// compartilhado com a conferência de coletabilidade e o resolvedor de metadado de fato.
/// </para>
/// </remarks>
internal static class ResolvedorValoresSelecionaveisCongelados
{
    private static readonly IReadOnlySet<string> NenhumFatoExcluido = new HashSet<string>(StringComparer.Ordinal);

    public static Result<IReadOnlyDictionary<string, ValoresDoCampo?>> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo) =>
        Resolver(processo, catalogo, NenhumFatoExcluido);

    /// <summary>
    /// Resolve os campos do processo, menos os dos fatos excluídos (os que o catálogo deixou de aceitar, recusados pela
    /// conferência de coletabilidade). A recusa de cada campo que não resolve é acumulada, na ordem do documento, e depois
    /// vêm as respostas fora das opções, conferidas sobre os campos que resolveram.
    /// </summary>
    internal static Result<IReadOnlyDictionary<string, ValoresDoCampo?>> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlySet<string> fatosExcluidos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(fatosExcluidos);

        IReadOnlyDictionary<string, string> caminhos = ItensDoFormularioNoDocumento.CaminhosPorFato(processo);
        CamposResolvidos campos = new ResolucaoDosCampos(processo, catalogo, caminhos).ResolverTodos(fatosExcluidos);
        IReadOnlyList<FieldError> recusas = [.. campos.Recusas, .. RespostasForaDasOpcoes(processo, campos.Valores, caminhos)];

        return recusas.Count > 0
            ? Result<IReadOnlyDictionary<string, ValoresDoCampo?>>.ValidationFailure(recusas)
            : Result<IReadOnlyDictionary<string, ValoresDoCampo?>>.Success(campos.Valores);
    }

    /// <summary>
    /// Toda resposta de um campo que forma as opções de outro precisa ser opção desse outro. As
    /// opções que o processo declara podem mudar depois de os campos serem definidos, e é aqui que
    /// as ofertas dos dois campos congelam juntas. Confere os campos que resolveram, na ordem do documento, e aponta as
    /// restrições do campo que forma as opções.
    /// </summary>
    private static IEnumerable<FieldError> RespostasForaDasOpcoes(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, ValoresDoCampo?> valoresPorFato, IReadOnlyDictionary<string, string> caminhos) =>
        ItensDoFormularioNoDocumento.EmOrdemDoDocumento(processo).Select(static c => c.Item)
            .Where(alvo => valoresPorFato.ContainsKey(alvo.FatoCodigo))
            .SelectMany(alvo => alvo.Restricoes.OfType<OpcoesDasRespostas>().Take(1)
                .SelectMany(respostas => FontesForaDasOpcoes(alvo, respostas, valoresPorFato, $"{caminhos[alvo.FatoCodigo]}.restricoes")));

    private static IEnumerable<FieldError> FontesForaDasOpcoes(
        FatoColetado alvo, OpcoesDasRespostas respostas, IReadOnlyDictionary<string, ValoresDoCampo?> valoresPorFato, string campo)
    {
        HashSet<string> oferta = [.. (valoresPorFato[alvo.FatoCodigo] ?? []).Select(static v => v.Codigo)];

        return respostas.Fatos
            .Where(f => valoresPorFato.GetValueOrDefault(f) is { } daFonte && daFonte.Any(v => !oferta.Contains(v.Codigo)))
            .Select(fonte => new FieldError(campo, new DomainError(
                ItemFormularioErrorCodes.OpcoesDeOutroDominio,
                $"As opções do campo '{fonte}' formam as opções do campo '{alvo.FatoCodigo}', mas nem todas são opções dele.")));
    }

    private sealed record CamposResolvidos(IReadOnlyDictionary<string, ValoresDoCampo?> Valores, IReadOnlyList<FieldError> Recusas);

    /// <summary>
    /// Os valores de cada campo do processo, resolvidos sobre o catálogo lido uma vez. A recusa de cada campo é acumulada no
    /// <c>fatoCodigo</c> do campo no documento, e o fato que o catálogo não resolve é defeito de quem chamou, não do processo.
    /// </summary>
    private sealed class ResolucaoDosCampos(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo, IReadOnlyDictionary<string, string> caminhos)
    {
        private readonly IReadOnlySet<(string Fato, string Valor)> _valoresCitados = ReferenciasVivas.Vinculos(processo.Declaracao()).Valores;

        public CamposResolvidos ResolverTodos(IReadOnlySet<string> fatosExcluidos)
        {
            Dictionary<string, ValoresDoCampo?> resolvidos = new(StringComparer.Ordinal);
            List<FieldError> recusas = [];
            foreach (FatoColetado fato in ItensDoFormularioNoDocumento.EmOrdemDoDocumento(processo).Select(static c => c.Item).Where(f => !fatosExcluidos.Contains(f.FatoCodigo)))
            {
                Result<ValoresDoCampo?> doCampo = ResolverCampo(fato);
                recusas.AddRange(doCampo.Errors);
                if (doCampo.IsSuccess)
                {
                    resolvidos[fato.FatoCodigo] = doCampo.Value;
                }
            }

            return new CamposResolvidos(resolvidos, recusas);
        }

        private Result<ValoresDoCampo?> ResolverCampo(FatoColetado fato)
        {
            if (!fato.TipoRenderizacao.EhEscolha())
            {
                return Result<ValoresDoCampo?>.Success(null);
            }

            Result<ValoresDoCampo> origem = ResolverOrigemDosValores(fato, NoCatalogo(fato));

            return origem.IsFailure
                ? Result<ValoresDoCampo?>.ValidationFailure([new FieldError($"{caminhos[fato.FatoCodigo]}.fatoCodigo", origem.Error!)])
                : Result<ValoresDoCampo?>.Success(origem.Value);
        }

        // Inalcançável em uso normal: a conferência de coletabilidade já recusou, no mesmo congelamento e sobre o MESMO catálogo,
        // o fato coletado que não resolve. Chegar aqui sem resolver seria os dois passos operando sobre catálogos diferentes.
        private FatoCandidatoView NoCatalogo(FatoColetado fato) =>
            catalogo.TryGetValue(fato.FatoCodigo, out FatoCandidatoView? fatoNoCatalogo)
                ? fatoNoCatalogo
                : throw new InvalidOperationException(
                    $"O fato coletado '{fato.FatoCodigo}' não resolve no catálogo — a conferência de coletabilidade deveria tê-lo recusado antes deste ponto.");

        // A origem é a copiada para o campo, a mesma que o agregado confere na publicação: o rascunho a recopia do catálogo ao
        // publicar, e depois de congelada ela não muda mais. O catálogo só responde pelo campo cuja origem é ele.
        private Result<ValoresDoCampo> ResolverOrigemDosValores(FatoColetado fato, FatoCandidatoView fatoNoCatalogo) =>
            RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(fato) switch
            {
                OrigemValoresColeta.OpcoesDoProcesso => Result<ValoresDoCampo>.Success(OpcoesDoProcesso(fato)),
                OrigemValoresColeta.MunicipiosDoFada => Result<ValoresDoCampo>.Success(MunicipiosDoBonus()),
                OrigemValoresColeta.MunicipiosDaProvaPresencial => Result<ValoresDoCampo>.Success(MunicipiosDaProvaPresencial()),
                OrigemValoresColeta.UnidadesFederativas => Result<ValoresDoCampo>.Success(UnidadesFederativas()),
                _ => DoCatalogo(fato, fatoNoCatalogo),
            };

        private ValoresDoCampo OpcoesDoProcesso(FatoColetado fato) =>
            [.. processo.OpcoesDoProcesso(fato.FatoCodigo).Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))];

        private ValoresDoCampo MunicipiosDoBonus() =>
            [.. VocabularioDeFatos.MunicipiosDoBonus(processo)
                .Select(static (m, ordem) => new ValorDominioDeclaradoCongelado(m.CodigoIbge, $"{m.Nome}/{m.Uf}", ordem))];

        private ValoresDoCampo MunicipiosDaProvaPresencial() =>
            [.. (processo.ProvaPresencial?.Municipios ?? [])
                .Select(static (m, ordem) => new ValorDominioDeclaradoCongelado(m.CodigoIbge, $"{m.Nome}/{m.Uf}", ordem))];

        private static ValoresDoCampo UnidadesFederativas() =>
            [.. ReferenciaCidadeGeo.UnidadesFederativas.Select(static (uf, ordem) => new ValorDominioDeclaradoCongelado(uf.Sigla, uf.Nome, ordem))];

        private Result<ValoresDoCampo> DoCatalogo(FatoColetado fato, FatoCandidatoView fatoNoCatalogo) =>
            fatoNoCatalogo.ValoresDominioDeclarados is { Count: > 0 }
                ? Declarados(fatoNoCatalogo)
                : Result<ValoresDoCampo>.Failure(SemTiposDeDeficiencia(fatoNoCatalogo) ?? SemOrigemDeValores(fato.FatoCodigo));

        // Ordenação canônica própria: o encoder não pode depender de o catálogo já vir ordenado, porque o catálogo valida
        // unicidade de Codigo, não de Ordem, e duas configurações equivalentes com empate produziriam bytes distintos sem o
        // desempate por código.
        private Result<ValoresDoCampo> Declarados(FatoCandidatoView fatoNoCatalogo)
        {
            ValoresDoCampo valores = [.. VocabularioDeFatos.ValoresVigentes(fatoNoCatalogo, _valoresCitados)
                .OrderBy(static v => v.Ordem)
                .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao, v.Ordem, v.Orientacao))];

            return valores.Count == 0
                ? Result<ValoresDoCampo>.Failure(SemValoresVigentes(fatoNoCatalogo))
                : Result<ValoresDoCampo>.Success(valores);
        }

        /// <summary>
        /// O fato cujos valores são os tipos do cadastro de tipos de deficiência não tem o que oferecer
        /// quando o cadastro não tem tipo vivo: a recusa diz onde corrigir, e não cai na de fonte desconhecida.
        /// </summary>
        private static DomainError? SemTiposDeDeficiencia(FatoCandidatoView fatoNoCatalogo) =>
            fatoNoCatalogo.FonteValoresValor == FonteValoresFato.TiposDeDeficiencia
                ? new DomainError(
                    "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                    $"O fato coletado '{fatoNoCatalogo.Codigo}' oferece os tipos do cadastro de tipos de deficiência, e o cadastro não tem nenhum tipo ativo.")
                : null;

        // O fato do catálogo sem nenhum valor vigente não tem o que oferecer. A oferta vazia do processo e a do bônus regional
        // são da conferência da configuração, que a recusa uma vez só.
        private static DomainError SemValoresVigentes(FatoCandidatoView fatoNoCatalogo) =>
            SemTiposDeDeficiencia(fatoNoCatalogo)
            ?? new DomainError(
                "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                $"O fato coletado '{fatoNoCatalogo.Codigo}' é de seleção, mas resolveu zero valores selecionáveis.");

        private static DomainError SemOrigemDeValores(string fatoCodigo) =>
            new(
                "ProcessoSeletivo.FatoDeSelecaoSemOrigemDeValores",
                $"O fato coletado '{fatoCodigo}' está vinculado como seleção, mas o catálogo não declara valores de domínio para ele "
                + "e a fonte dos seus valores não é o processo nem o bônus regional — não há de onde derivar os valores selecionáveis.");
    }
}
