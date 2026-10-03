namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.RegularExpressions;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Copia o modelo de formulário para o processo pelas mesmas conferências da escrita direta: as
/// etapas, os itens e os termos passam pelos resolvedores da escrita do formulário, e o estado
/// final é conferido pelo agregado, que só então muda o processo (ADR-0125).
/// </summary>
/// <remarks>
/// <para>
/// Antes da cópia, o modelo precisa estar ativo, servir ao tipo do processo e ter os pressupostos
/// coletados pela inscrição. O que o catálogo mudou depois da composição sai da cópia e volta no
/// relatório: o item de fato desativado — salvo o que o processo já vincula, que não é vínculo novo
/// (ADR-0136) — ou não mais coletável, e o termo cuja versão foi removida. O valor desativado citado
/// numa regra é recusado como vínculo novo.
/// </para>
/// <para>
/// O fato que outra finalidade já coleta segue a regra do par de finalidades, porque só a inscrição
/// é citável pelas outras (UNI-REQ-0144). Na inscrição, ele passa para a cópia. Nas outras, o que
/// está na inscrição sai da cópia, e o que está em outro formulário é recusado pelo agregado.
/// </para>
/// <para>
/// Os derivados por regra que a cópia cita, direta ou transitivamente, recebem do catálogo as
/// regras padrão quando o processo ainda não os configura. A configuração existente, como a da
/// modalidade, nunca é trocada.
/// </para>
/// </remarks>
public static partial class AplicarModeloFormularioCommandHandler
{
    private const string Item = "ITEM";
    private const string Termo = "TERMO";
    private const string Grupo = "GRUPO";
    private const string FatoDesativado = "FATO_DESATIVADO";
    private const string FatoNaoColetavel = "FATO_NAO_COLETAVEL";
    private const string VersaoDeTermoRemovida = "VERSAO_DE_TERMO_REMOVIDA";
    private const string FonteModalidade = "MODALIDADE";

    public static async Task<Result<AplicacaoDeModeloDto>> Handle(
        AplicarModeloFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IModeloFormularioReader modeloFormularioReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(modeloFormularioReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(termoConsentimentoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<AplicacaoDeModeloDto>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // O estado do processo é conferido antes de qualquer leitura externa.
        if (processo.RecusaDeAplicacaoDeModelo(command.Precondicao) is { } recusaDeEstado)
        {
            return Result<AplicacaoDeModeloDto>.Failure(recusaDeEstado);
        }

        ModeloFormularioView? modelo = await modeloFormularioReader.ObterAsync(command.ModeloId, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure([new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.ModeloInexistente, "O modelo de formulário informado não existe."))]);
        }

        List<FieldError> erros = [.. ConferirModelo(modelo, processo)];
        if (erros.Count > 0)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(erros);
        }

        FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(modelo.Finalidade);
        ConteudoDoModeloInput conteudo = modelo.Conteudo;
        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogo);
        Relatorio relatorio = new();

        // Itens: o que o catálogo mudou sai da cópia, salvo o fato desativado que o processo já
        // vincula, que continua com ele (ADR-0136); o fato de outra finalidade segue o par.
        IReadOnlySet<string> vinculados = processo.Vinculos().Fatos;
        List<int> indicesDosItens = [];
        List<FatoColetadoInput> itensACopiar = [];
        IReadOnlyList<FatoColetadoInput> itensDoModelo = conteudo.Itens ?? [];
        IReadOnlyList<GrupoColetadoInput> gruposDoModelo = conteudo.Grupos ?? [];
        IReadOnlyList<EtapaFormularioInput> etapasDoModelo = conteudo.Etapas ?? [];

        // O formulário de inscrição coleta o conjunto básico na seção reservada: o básico que o
        // modelo não traz entra como o conjunto básico o define.
        if (finalidade == FinalidadeFormulario.Inscricao)
        {
            (itensDoModelo, gruposDoModelo, List<FieldError> mescla) =
                ConjuntoBasicoDaInscricao.MesclarItens(itensDoModelo, gruposDoModelo, ConjuntoBasicoDaInscricao.Referencia(itensDoModelo));
            (etapasDoModelo, List<FieldError> mesclaDasEtapas) =
                ConjuntoBasicoDaInscricao.MesclarEtapas(etapasDoModelo, ConjuntoBasicoDaInscricao.SecaoDe(etapasDoModelo));
            erros.AddRange(mescla.Concat(mesclaDasEtapas).Select(static e => NoModelo(e)));
        }

        for (int i = 0; i < itensDoModelo.Count; i++)
        {
            FatoColetadoInput item = itensDoModelo[i];
            if (DescarteDoCampo(item.FatoCodigo, contexto, vinculados, ConferenciaNoCatalogo.EhColetavel) is { } motivo)
            {
                relatorio.Descartados.Add(new ParteDescartadaDto(Item, item.FatoCodigo, motivo));
                continue;
            }

            FatoColetado? emOutra = processo.FatosColetados.FirstOrDefault(f => f.FatoCodigo == item.FatoCodigo && f.Finalidade != finalidade);
            if (emOutra is not null && finalidade == FinalidadeFormulario.Inscricao)
            {
                relatorio.Trazidos.Add(item.FatoCodigo);
            }
            else if (emOutra?.Finalidade == FinalidadeFormulario.Inscricao)
            {
                relatorio.Mantidos.Add(item.FatoCodigo);
                continue;
            }

            indicesDosItens.Add(i);
            itensACopiar.Add(item);
        }

        (List<FatoColetado> itens, List<FieldError> errosDosItens) = EscritaDosItens.Resolver(EscritaDosItens.Ler(itensACopiar), contexto);
        erros.AddRange(errosDosItens.Select(e => NoModelo(e, "itens", indicesDosItens)));

        // Grupos: o grupo com campo que o catálogo mudou sai da cópia inteiro — a ocorrência sem o
        // campo seria outra lista que o modelo não compôs.
        List<int> indicesDosGrupos = [];
        List<GrupoColetadoInput> gruposACopiar = [];
        for (int i = 0; i < gruposDoModelo.Count; i++)
        {
            GrupoColetadoInput grupo = gruposDoModelo[i];
            if ((grupo.Subitens ?? []).Select(s => DescarteDoCampo(s?.FatoCodigo, contexto, vinculados, ConferenciaNoCatalogo.EhColetavelEmGrupo))
                    .FirstOrDefault(static m => m is not null) is { } motivo)
            {
                relatorio.Descartados.Add(new ParteDescartadaDto(Grupo, grupo.Codigo, motivo));
                continue;
            }

            indicesDosGrupos.Add(i);
            gruposACopiar.Add(grupo);
        }

        (List<GrupoColetado> grupos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(EscritaDosItens.LerGrupos(gruposACopiar), contexto);
        erros.AddRange(errosDosGrupos.Select(e => NoModelo(e, "grupos", indicesDosGrupos)));

        EtapasLidas etapas = EscritaDasEtapas.Ler(etapasDoModelo);
        erros.AddRange(etapas.Erros.Concat(EscritaDasEtapas.ConferirExibicoes(etapas, contexto)).Select(static e => NoModelo(e)));

        // Termos: a versão removida do catálogo sai da cópia.
        IReadOnlyList<TermoExigidoInput> termosDoModelo = conteudo.Termos ?? [];
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoConsentimentoReader
            .ListarVersoesAsync([.. termosDoModelo.Select(static t => t.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, VersaoTermoConsentimentoView> versaoPorId = versoes.ToDictionary(static v => v.VersaoId);
        List<int> indicesDosTermos = [];
        List<TermoExigidoInput> termosACopiar = [];
        for (int i = 0; i < termosDoModelo.Count; i++)
        {
            TermoExigidoInput termo = termosDoModelo[i];
            if (!versaoPorId.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) || versao.TermoId != termo.TermoId)
            {
                relatorio.Descartados.Add(new ParteDescartadaDto(Termo, termo.Codigo, VersaoDeTermoRemovida));
                continue;
            }

            indicesDosTermos.Add(i);
            termosACopiar.Add(termo);
        }

        TermosLidos termosLidos = EscritaDosTermos.Ler(termosACopiar);

        // Derivações: as que a cópia cita e o processo ainda não configura vêm do catálogo.
        IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>> regrasPadrao =
            await fatoCandidatoReader.ListarRegrasPadraoAsync(cancellationToken).ConfigureAwait(false);
        // Os citados vêm da entrada das partes que entram na cópia, inclusive das que serão recusadas:
        // o que falta configurar aparece junto das outras recusas, e o que saiu da cópia não pesa.
        string[] citados = [.. FatosCitados(itensACopiar, gruposACopiar, conteudo.Etapas ?? [], termosACopiar).Distinct(StringComparer.Ordinal)];
        List<ConfiguracaoDerivacaoInput> derivacoesACopiar = EscolherDerivacoes(citados, processo, contexto, regrasPadrao, relatorio, erros);

        // A modalidade pode vir pela cópia ou pelas regras padrão que ela traz.
        IEnumerable<string> fechamento = citados.Concat(derivacoesACopiar
            .SelectMany(static d => d.Regras).SelectMany(static r => r.Quando ?? []).SelectMany(static c => c).Select(static c => c.Fato));
        if (processo.DistribuicaoVagas.Count == 0
            && fechamento.Any(c => contexto.Fatos.TryGetValue(c, out FatoCandidatoView? fato) && fato.FonteValores == FonteModalidade))
        {
            erros.Add(new("modelo", new DomainError(
                AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas,
                "O modelo cita a modalidade de concorrência, que depende da distribuição de vagas do processo; defina a distribuição de vagas antes.")));
        }

        // O universo da configuração final: o que o processo coleta e deriva depois da cópia.
        HashSet<string> universo = new(
            processo.FatosColetados.Select(static f => f.FatoCodigo)
                .Concat(itens.Select(static i => i.FatoCodigo))
                .Concat(processo.RegrasDerivacao.Select(static r => r.CodigoFato))
                .Concat(derivacoesACopiar.Select(static d => d.CodigoFato)),
            StringComparer.Ordinal);
        List<ConfiguracaoDerivacaoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoInput derivacao in derivacoesACopiar)
        {
            Result<ConfiguracaoDerivacaoFato> resolvida = DefinirRegrasDerivacaoCommandHandler.ResolverConfiguracao(
                derivacao, contexto.Fatos, contexto.Vocabulario, universo, contexto.DominiosDinamicos);
            if (resolvida.IsSuccess)
            {
                derivacoes.Add(resolvida.Value!);
            }
            else
            {
                erros.AddRange(resolvida.Errors.Select(e => e with { Field = $"derivacoes[{derivacao.CodigoFato}]" }));
            }
        }

        // O termo cita também o derivado do sistema cujas dependências o processo coleta depois da
        // cópia; a regra de derivação, não.
        HashSet<string> coletados = new(processo.FatosColetados.Select(static f => f.FatoCodigo).Concat(itens.Select(static i => i.FatoCodigo)), StringComparer.Ordinal);
        (List<TermoExigidoFormulario> termos, List<FieldError> errosDosTermos) =
            EscritaDosTermos.Resolver(
                termosLidos, contexto, versaoPorId, new HashSet<string>(universo.Concat(VocabularioDeFatos.DerivadosDoSistemaResolvidos(coletados)), StringComparer.Ordinal));
        erros.AddRange(errosDosTermos.Select(e => NoModelo(e, "termos", indicesDosTermos)));
        if (erros.Count > 0)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(erros);
        }

        // O valor desativado no catálogo é recusado como vínculo novo; o que o processo já citava continua.
        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            processo.Vinculos(),
            VinculosDeFatos.De(
                itens.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static i => i.FatoCodigo).Concat(derivacoes.Select(static d => d.CodigoFato)),
                itens.SelectMany(static i => i.Condicoes)
                    .Concat(grupos.SelectMany(static g => g.Condicoes))
                    .Concat(etapas.Etapas.SelectMany(static e => e.Condicoes))
                    .Concat(termos.SelectMany(static t => t.Condicoes))
                    .Select(static c => (c.Fato, c.Valor))
                    .Concat(derivacoes.SelectMany(static d => d.Regras).SelectMany(static r => r.Condicoes).Select(static c => (c.Fato, c.Valor))),
                derivacoes.SelectMany(static d => d.CodigosContribuidos)));
        if (vinculoNovo.IsFailure)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(vinculoNovo.Errors.Select(static e => NoModelo(e)).ToList());
        }

        Result aplicar = processo.AplicarModeloDeFormulario(
            new CopiaDeModeloDeFormulario(finalidade, conteudo.Titulo, etapas.Etapas, itens, grupos, termos, derivacoes, modelo.Id, modelo.Codigo),
            command.Precondicao);
        if (aplicar.IsFailure)
        {
            // O agregado indexa a cópia, sem o que saiu dela: cada recusa volta à posição no modelo.
            return Result<AplicacaoDeModeloDto>.ValidationFailure(
                aplicar.Errors.Select(e => NoModelo(NaPosicaoDoModelo(
                    NaPosicaoDoModelo(NaPosicaoDoModelo(e, "itens", indicesDosItens), "grupos", indicesDosGrupos), "termos", indicesDosTermos))).ToList());
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<AplicacaoDeModeloDto>.Success(new AplicacaoDeModeloDto(
            modelo.Finalidade, relatorio.Trazidos, relatorio.Mantidos, relatorio.Descartados, relatorio.Copiadas, relatorio.Mantidas));
    }

    /// <summary>
    /// O modelo serve a este processo: está ativo, é do tipo dele ou de todos os tipos, e os
    /// pressupostos — fatos da inscrição que ele cita — são coletados pela inscrição do processo.
    /// </summary>
    private static IEnumerable<FieldError> ConferirModelo(ModeloFormularioView modelo, ProcessoSeletivo processo)
    {
        if (!modelo.Ativo)
        {
            yield return new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.ModeloInativo, $"O modelo '{modelo.Codigo}' está desativado e não se aplica a processo novo."));
        }

        if (modelo.TipoProcessoCodigo is { } tipo && !string.Equals(tipo, processo.TipoProcesso.Codigo, StringComparison.Ordinal))
        {
            yield return new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.TipoDeProcessoDiferente,
                $"O modelo '{modelo.Codigo}' é do tipo de processo '{tipo}', e este processo é '{processo.TipoProcesso.Codigo}'."));
        }

        HashSet<string> daInscricao = new(
            processo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.FatoCodigo),
            StringComparer.Ordinal);
        IReadOnlyList<string> pressupostos = modelo.Conteudo.Pressupostos ?? [];
        for (int i = 0; i < pressupostos.Count; i++)
        {
            if (!daInscricao.Contains(pressupostos[i]))
            {
                yield return new($"modelo.pressupostos[{i}]", new DomainError(
                    AplicacaoDeModeloErrorCodes.PressupostoAusente,
                    $"O modelo cita '{pressupostos[i]}', que a inscrição do processo não coleta; colete-o na inscrição antes."));
            }
        }
    }

    /// <summary>Os fatos que as regras das partes citam, na entrada: exibições, obrigatoriedades e restrições.</summary>
    private static IEnumerable<string> FatosCitados(
        IEnumerable<FatoColetadoInput> itens,
        IEnumerable<GrupoColetadoInput> grupos,
        IEnumerable<EtapaFormularioInput> etapas,
        IEnumerable<TermoExigidoInput> termos) =>
        itens.Concat(grupos.SelectMany(static g => g.Subitens ?? []))
            .SelectMany(static i => Citados(i?.Precondicao).Concat(Citados(i?.PredicadoObrigatoriedade))
                .Concat((i?.Restricoes ?? []).SelectMany(static r => (r.Fatos ?? []).Concat((r.Entradas ?? []).SelectMany(static e => Citados(e.Quando))))))
            .Concat(grupos.SelectMany(static g => Citados(g.Exibicao).Concat(Citados(g.PredicadoObrigatoriedade))))
            .Concat(etapas.SelectMany(static e => Citados(e?.Exibicao)))
            .Concat(termos.SelectMany(static t => Citados(t.Exibicao).Concat(Citados(t.PredicadoObrigatoriedade))));

    private static IEnumerable<string> Citados(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado) =>
        (predicado ?? []).SelectMany(static clausula => clausula ?? []).Select(static c => c?.Fato).OfType<string>();

    /// <summary>
    /// O motivo de o campo — item ou campo de grupo, conforme <paramref name="coletavel"/> — sair da
    /// cópia, quando o catálogo mudou depois da composição do modelo. O fato desativado que o processo
    /// já vincula não é vínculo novo e fica.
    /// </summary>
    private static string? DescarteDoCampo(
        string? fatoCodigo, ContextoDoCatalogo contexto, IReadOnlySet<string> vinculados, Func<FatoDoCatalogo, bool> coletavel)
    {
        if (fatoCodigo is null || !contexto.FatosDasRegras.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato) || !coletavel(fato))
        {
            return FatoNaoColetavel;
        }

        return fato.Ativo || vinculados.Contains(fatoCodigo) ? null : FatoDesativado;
    }

    /// <summary>
    /// Os derivados por regra citados pela cópia, direta ou transitivamente: o que o processo já
    /// configura fica como está; o que não configura recebe as regras padrão do catálogo, salvo o
    /// derivado sem regra padrão, que o processo precisa configurar antes.
    /// </summary>
    private static List<ConfiguracaoDerivacaoInput> EscolherDerivacoes(
        IEnumerable<string> citados,
        ProcessoSeletivo processo,
        ContextoDoCatalogo contexto,
        IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>> regrasPadrao,
        Relatorio relatorio,
        List<FieldError> erros)
    {
        HashSet<string> configurados = new(processo.RegrasDerivacao.Select(static r => r.CodigoFato), StringComparer.Ordinal);
        HashSet<string> vistos = new(StringComparer.Ordinal);
        Queue<string> pendentes = new(citados.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        List<ConfiguracaoDerivacaoInput> aCopiar = [];
        while (pendentes.TryDequeue(out string? codigo))
        {
            if (!vistos.Add(codigo)
                || !contexto.Fatos.TryGetValue(codigo, out FatoCandidatoView? fato)
                || !VinculoDeFato.Usa(fato.Binding, VinculoDeFato.RegraDeDerivacao))
            {
                continue;
            }

            if (configurados.Contains(codigo))
            {
                relatorio.Mantidas.Add(codigo);
            }
            else if (!regrasPadrao.TryGetValue(codigo, out IReadOnlyList<RegraDerivacao>? regras))
            {
                erros.Add(new($"derivacoes[{codigo}]", new DomainError(
                    AplicacaoDeModeloErrorCodes.DerivadoSemRegra,
                    $"O modelo cita '{codigo}', que o processo ainda não deriva e o catálogo não tem regra padrão; configure as regras de derivação dele no processo antes.")));
            }
            else
            {
                relatorio.Copiadas.Add(codigo);
                aCopiar.Add(new ConfiguracaoDerivacaoInput(codigo, [.. regras.Select(static (r, ordem) => new RegraDerivacaoInput(ordem, r.Contribui, Quando(r.Quando)))]));
                foreach (string dependencia in regras.SelectMany(static r => r.FatosCitados))
                {
                    pendentes.Enqueue(dependencia);
                }
            }
        }

        return aCopiar;
    }

    /// <summary>O predicado da regra padrão na forma da entrada de derivação; a regra âncora não tem condição.</summary>
    private static IReadOnlyList<IReadOnlyList<CondicaoDerivacaoInput>>? Quando(PredicadoDnf quando) =>
        EntradaDeRegras.ParaEntrada(quando) is { Count: > 0 } clausulas
            ? [.. clausulas.Select(static c => (IReadOnlyList<CondicaoDerivacaoInput>)[.. c.Select(static k => new CondicaoDerivacaoInput(k.Fato, k.Operador, k.Valor))])]
            : null;

    /// <summary>
    /// A recusa aponta a parte do modelo: o índice que o resolvedor deu à lista copiada volta à
    /// posição da parte no modelo.
    /// </summary>
    private static FieldError NoModelo(FieldError erro, string lista, List<int> indices) =>
        NoModelo(NaPosicaoDoModelo(erro, lista, indices));

    private static FieldError NoModelo(FieldError erro) =>
        erro with { Field = string.IsNullOrEmpty(erro.Field) ? "modelo" : $"modelo.{erro.Field}" };

    private static FieldError NaPosicaoDoModelo(FieldError erro, string lista, List<int> indices)
    {
        string campo = erro.Field ?? string.Empty;
        if (IndiceDaLista().Match(campo) is not { Success: true } posicao
            || !string.Equals(posicao.Groups["lista"].Value, lista, StringComparison.Ordinal))
        {
            return erro;
        }

        int indice = int.Parse(posicao.Groups["indice"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return erro with { Field = $"{lista}[{indices[indice]}]{campo[posicao.Length..]}" };
    }

    [GeneratedRegex(@"^(?<lista>[a-z]+)\[(?<indice>\d+)\]")]
    private static partial Regex IndiceDaLista();

    private sealed class Relatorio
    {
        public List<string> Trazidos { get; } = [];
        public List<string> Mantidos { get; } = [];
        public List<ParteDescartadaDto> Descartados { get; } = [];
        public List<string> Copiadas { get; } = [];
        public List<string> Mantidas { get; } = [];
    }
}
