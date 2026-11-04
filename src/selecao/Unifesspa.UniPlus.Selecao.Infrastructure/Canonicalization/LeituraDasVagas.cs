namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da distribuição de vagas: ofertas, quadro de vagas, referência demográfica e opções de curso.
/// </summary>
internal static class LeituraDasVagas
{
    /// <summary>
    /// Lê o bloco do número de opções de curso. O intervalo é o mesmo que o agregado exige ao
    /// declará-lo: um envelope adulterado com outro valor é malformado, e não reidratado como se o
    /// domínio o aceitasse.
    /// </summary>
    internal static int? LerOpcoesDeCurso(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "opcoesDeCurso", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "opcoesDeCurso", "numero");
        int numero = leitor.Inteiro(bloco, "numero", "opcoesDeCurso");
        if (leitor.Falhou)
        {
            return null;
        }

        if (numero is < 1 or > ProcessoSeletivo.MaximoDeOpcoesDeCurso)
        {
            return leitor.Propagar<int?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'opcoesDeCurso.numero' congelado deve ser 1 ou {ProcessoSeletivo.MaximoDeOpcoesDeCurso}."));
        }

        return numero;
    }

    /// <summary>
    /// Recombina os <b>três blocos derivados</b> da mesma coleção — <c>distribuicao</c>,
    /// <c>modalidades</c> e <c>ofertas</c> (ADR-0110 D8). A exigência é <b>igualdade
    /// exata de conjuntos</b> de <c>ofertaCursoOrigemId</c>, não mera inclusão: recombinar
    /// em silêncio um envelope incoerente reconstruiria um agregado que <b>nunca
    /// existiu</b> — uma modalidade sem oferta, uma oferta sem vagas.
    /// </summary>
    internal static IReadOnlyList<ConfiguracaoDistribuicaoVagas> LerDistribuicao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray arrayDistribuicao = leitor.Array(payload, "distribuicao", "$");
        JsonArray arrayModalidades = leitor.Array(payload, "modalidades", "$");
        JsonArray arrayVagas = leitor.Array(payload, "vagas", "$");
        List<(Guid Id, OfertaCursoSnapshot Oferta)> ofertasDeclaradas = LerOfertas(leitor, payload);
        if (leitor.Falhou)
        {
            return [];
        }

        List<Guid> ofertasEmVagas = LerOfertasDoQuadro(leitor, arrayVagas);
        ModalidadesPorOferta modalidades = LerModalidadesPorOferta(leitor, arrayModalidades);
        List<InsumoDaDistribuicao> insumos = LerInsumos(leitor, arrayDistribuicao);
        if (leitor.Falhou)
        {
            return [];
        }

        if (VerificarBlocosDerivados([.. insumos.Select(static i => i.Oferta)], modalidades.Ofertas, ofertasEmVagas, [.. ofertasDeclaradas.Select(static o => o.Id)]) is { } incoerencia)
        {
            return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(incoerencia) ?? [];
        }

        return CriarConfiguracoes(leitor, insumos, modalidades.PorOferta, ofertasDeclaradas.ToDictionary(static o => o.Id, static o => o.Oferta));
    }

    /// <summary>
    /// O bloco de vagas é validado só por forma: cada entrada existe, tem as chaves certas e cobre a
    /// mesma oferta que a distribuição. O quadro em si é recomputado a partir dos insumos, porque ler e
    /// injetar o valor congelado tornaria a prova de reprodutibilidade tautológica.
    /// </summary>
    private static List<Guid> LerOfertasDoQuadro(LeitorEnvelope leitor, JsonArray arrayVagas)
    {
        List<Guid> ofertas = [];
        for (int i = 0; !leitor.Falhou && i < arrayVagas.Count; i++)
        {
            string path = $"vagas[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayVagas, i, "vagas");
            leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "quadro", "vrNominal", "vrFinal", "estouro", "capadoEmVo", "totalPublicado");

            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            JsonArray quadro = leitor.Array(item, "quadro", path);
            leitor.Inteiro(item, "vrNominal", path);
            leitor.Inteiro(item, "vrFinal", path);
            leitor.Inteiro(item, "estouro", path);
            leitor.Booleano(item, "capadoEmVo", path);
            leitor.Inteiro(item, "totalPublicado", path);
            if (!leitor.Falhou)
            {
                ConferirLinhasDoQuadro(leitor, quadro, path);
            }

            ofertas.Add(ofertaId);
        }

        return ofertas;
    }

    private static void ConferirLinhasDoQuadro(LeitorEnvelope leitor, JsonArray quadro, string path)
    {
        for (int j = 0; j < quadro.Count; j++)
        {
            string quadroPath = $"{path}.quadro[{j}]";
            JsonObject linha = leitor.ItemObjeto(quadro, j, $"{path}.quadro");
            leitor.ExigirChaves(linha, quadroPath, "modalidadeCodigo", "quantidade");
            leitor.TextoNaoVazio(linha, "modalidadeCodigo", quadroPath, LimitesDoEnvelope.ModalidadeCodigo);
            leitor.Inteiro(linha, "quantidade", quadroPath);
        }
    }

    private static ModalidadesPorOferta LerModalidadesPorOferta(LeitorEnvelope leitor, JsonArray arrayModalidades)
    {
        Dictionary<Guid, List<ModalidadeSelecionada>> porOferta = [];
        List<Guid> ofertas = [];
        for (int i = 0; !leitor.Falhou && i < arrayModalidades.Count; i++)
        {
            string path = $"modalidades[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayModalidades, i, "modalidades");
            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            ModalidadeSelecionada? modalidade = LeituraDasModalidades.LerModalidade(leitor, item, path);
            if (leitor.Falhou)
            {
                break;
            }

            ofertas.Add(ofertaId);
            if (!porOferta.TryGetValue(ofertaId, out List<ModalidadeSelecionada>? lista))
            {
                lista = [];
                porOferta[ofertaId] = lista;
            }

            lista.Add(modalidade!);
        }

        return new ModalidadesPorOferta(porOferta, ofertas);
    }

    private static List<InsumoDaDistribuicao> LerInsumos(LeitorEnvelope leitor, JsonArray arrayDistribuicao)
    {
        List<InsumoDaDistribuicao> insumos = [];
        for (int i = 0; !leitor.Falhou && i < arrayDistribuicao.Count; i++)
        {
            InsumoDaDistribuicao? insumo = LerInsumo(leitor, leitor.ItemObjeto(arrayDistribuicao, i, "distribuicao"), $"distribuicao[{i}]");
            if (insumo is not { } lido)
            {
                break;
            }

            insumos.Add(lido);
        }

        return insumos;
    }

    private static InsumoDaDistribuicao? LerInsumo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "voBase", "pr", "regraDistribuicao", "regraAjuste", "referenciaDemografica", "grupoAreaEnem");

        Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
        int voBase = leitor.Inteiro(item, "voBase", path);
        decimal pr = leitor.Decimal(item, "pr", EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPr);

        // O rol deriva do catálogo de regras, não de literais: uma regra publicada sob qualquer código
        // do catálogo tem de reidratar, e uma lista escrita à mão deixaria de fora a que não a acompanhasse.
        ReferenciaRegra regra = leitor.Regra(item, "regraDistribuicao", path, [.. RegraDistribuicaoVagasCodigo.Todos]);
        ReferenciaRegra? regraAjuste = leitor.RegraOpcional(item, "regraAjuste", path, RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico);
        ReferenciaReservaDemograficaSnapshot? demografica = LerReferenciaDemografica(leitor, item, path);
        (string? Codigo, string? Rotulo)? grupoAreaEnem = LerGrupoAreaEnem(leitor, item, path);

        return leitor.Falhou
            ? null
            : new InsumoDaDistribuicao(ofertaId, voBase, pr, regra, regraAjuste, demografica, grupoAreaEnem);
    }

    private static IReadOnlyList<ConfiguracaoDistribuicaoVagas> CriarConfiguracoes(
        LeitorEnvelope leitor,
        List<InsumoDaDistribuicao> insumos,
        Dictionary<Guid, List<ModalidadeSelecionada>> modalidadesPorOferta,
        Dictionary<Guid, OfertaCursoSnapshot> nomePorOferta)
    {
        List<ConfiguracaoDistribuicaoVagas> distribuicao = [];
        foreach (InsumoDaDistribuicao insumo in insumos)
        {
            Result<ConfiguracaoDistribuicaoVagas> configuracao = ConfiguracaoDistribuicaoVagas.Criar(
                insumo.Oferta, insumo.VoBase, insumo.Pr, insumo.Regra, insumo.RegraAjuste, insumo.Demografica, modalidadesPorOferta[insumo.Oferta],
                modalidadesAdmitidas: RegraDistribuicaoVagasCodigo.RolFechado(insumo.Regra.Codigo),
                grupoAreaEnem: insumo.GrupoAreaEnem,
                ofertaCurso: nomePorOferta[insumo.Oferta]);
            if (configuracao.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(configuracao.Error!) ?? [];
            }

            distribuicao.Add(configuracao.Value!);
        }

        return distribuicao;
    }

    private readonly record struct ModalidadesPorOferta(Dictionary<Guid, List<ModalidadeSelecionada>> PorOferta, List<Guid> Ofertas);

    private readonly record struct InsumoDaDistribuicao(
        Guid Oferta,
        int VoBase,
        decimal Pr,
        ReferenciaRegra Regra,
        ReferenciaRegra? RegraAjuste,
        ReferenciaReservaDemograficaSnapshot? Demografica,
        (string? Codigo, string? Rotulo)? GrupoAreaEnem);

    /// <summary>
    /// Cada oferta de curso com o que a nomeia para o candidato, congelado na definição da
    /// distribuição.
    /// </summary>
    internal static List<(Guid Id, OfertaCursoSnapshot Oferta)> LerOfertas(LeitorEnvelope leitor, JsonObject payload)
    {
        if (leitor.Falhou)
        {
            return [];
        }

        JsonArray array = leitor.Array(payload, "ofertas", "$");
        List<(Guid, OfertaCursoSnapshot)> ofertas = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            (Guid, OfertaCursoSnapshot)? oferta = LerOferta(leitor, leitor.ItemObjeto(array, i, "ofertas"), $"ofertas[{i}]");
            if (oferta is not { } lida)
            {
                return leitor.Falhou ? ofertas : [];
            }

            ofertas.Add(lida);
        }

        return ofertas;
    }

    private static (Guid, OfertaCursoSnapshot)? LerOferta(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "cursoNome", "cursoGrau", "unidadeOfertanteNome", "turnos");
        Guid id = leitor.Identificador(item, "ofertaCursoOrigemId", path);
        string cursoNome = leitor.TextoNaoVazio(item, "cursoNome", path, OfertaCursoSnapshot.CursoNomeMaxLength);
        string cursoGrau = leitor.TextoNaoVazio(item, "cursoGrau", path, OfertaCursoSnapshot.CursoGrauMaxLength);
        string unidade = leitor.TextoNaoVazio(item, "unidadeOfertanteNome", path, OfertaCursoSnapshot.UnidadeOfertanteNomeMaxLength);
        IReadOnlyList<string> turnos = leitor.Textos(item, "turnos", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<OfertaCursoSnapshot> oferta = OfertaCursoSnapshot.Criar(cursoNome, cursoGrau, unidade, turnos);
        if (oferta.IsFailure)
        {
            leitor.Propagar<OfertaCursoSnapshot>(oferta.Error!);
            return null;
        }

        return (id, oferta.Value!);
    }

    internal static DomainError? VerificarBlocosDerivados(
        List<Guid> emDistribuicao,
        List<Guid> emModalidades,
        List<Guid> emVagas,
        List<Guid> ofertas)
    {
        if (RepeticaoDeOferta(emDistribuicao, "O bloco 'distribuicao' repete uma oferta de curso — cada oferta tem no máximo uma distribuição.") is { } repetidaNaDistribuicao)
        {
            return repetidaNaDistribuicao;
        }

        if (RepeticaoDeOferta(emVagas, "O bloco 'vagas' repete uma oferta de curso — cada oferta tem no máximo um quadro.") is { } repetidaNasVagas)
        {
            return repetidaNasVagas;
        }

        if (RepeticaoDeOferta(ofertas, "O bloco 'ofertas' repete uma oferta de curso.") is { } repetidaNasOfertas)
        {
            return repetidaNasOfertas;
        }

        HashSet<Guid> conjuntoDistribuicao = [.. emDistribuicao];
        return conjuntoDistribuicao.SetEquals(ofertas)
            && conjuntoDistribuicao.SetEquals(emModalidades)
            && conjuntoDistribuicao.SetEquals(emVagas)
            ? null
            : new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "Os blocos 'distribuicao', 'modalidades', 'vagas' e 'ofertas' derivam da mesma coleção e não declaram o mesmo conjunto de ofertas de curso (ADR-0110 D8).");
    }

    private static DomainError? RepeticaoDeOferta(List<Guid> ofertas, string mensagem) =>
        ofertas.Distinct().Count() != ofertas.Count
            ? new DomainError(ErrosCodecEnvelope.BlocosDerivadosIncoerentes, mensagem)
            : null;

    internal static ReferenciaReservaDemograficaSnapshot? LerReferenciaDemografica(
        LeitorEnvelope leitor,
        JsonObject distribuicao,
        string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "referenciaDemografica", pathPai);
        if (leitor.Falhou || item is null)
        {
            return null;
        }

        string path = $"{pathPai}.referenciaDemografica";
        leitor.ExigirChaves(item, path, "origemId", "censoReferencia", "ppiPercentual", "quilombolaPercentual", "pcdPercentual", "baseLegal");

        Guid origemId = leitor.Identificador(item, "origemId", path);
        string censo = leitor.TextoNaoVazio(item, "censoReferencia", path, LimitesDoEnvelope.CensoReferencia);
        decimal ppi = leitor.Decimal(item, "ppiPercentual", EscritaCanonica.EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal quilombola = leitor.Decimal(item, "quilombolaPercentual", EscritaCanonica.EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal pcd = leitor.Decimal(item, "pcdPercentual", EscritaCanonica.EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);

        if (leitor.Falhou)
        {
            return null;
        }

        Result<ReferenciaReservaDemograficaSnapshot> referencia =
            ReferenciaReservaDemograficaSnapshot.Criar(origemId, censo, ppi, quilombola, pcd, baseLegal);

        return referencia.IsFailure
            ? leitor.Propagar<ReferenciaReservaDemograficaSnapshot>(referencia.Error!)
            : referencia.Value;
    }

    /// <summary>
    /// O grupo de área do ENEM congelado na distribuição: <see langword="null"/> quando o
    /// curso da oferta não o declara, senão o objeto que <see cref="LerCodigoERotuloDoGrupoAreaEnem"/>
    /// lê. As invariantes do grupo são da factory da distribuição, que recebe o par cru.
    /// </summary>
    internal static (string? Codigo, string? Rotulo)? LerGrupoAreaEnem(
        LeitorEnvelope leitor,
        JsonObject distribuicao,
        string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "grupoAreaEnem", pathPai);
        return leitor.Falhou || item is null
            ? null
            : LerCodigoERotuloDoGrupoAreaEnem(leitor, item, $"{pathPai}.grupoAreaEnem");
    }

    /// <summary>O grupo de área do ENEM obrigatório, como no quadro de pesos por área da classificação.</summary>
    internal static (string Codigo, string Rotulo)? LerGrupoAreaEnemObrigatorio(
        LeitorEnvelope leitor,
        JsonObject pai,
        string pathPai)
    {
        JsonObject item = leitor.Objeto(pai, "grupoAreaEnem", pathPai);
        return leitor.Falhou ? null : LerCodigoERotuloDoGrupoAreaEnem(leitor, item, $"{pathPai}.grupoAreaEnem");
    }

    /// <summary>
    /// O objeto <c>grupoAreaEnem</c> já obtido — <c>{codigo, rotulo}</c>, os dois obrigatórios e
    /// dentro do limite das colunas —, a mesma forma na distribuição de vagas e no quadro de
    /// pesos por área da classificação.
    /// </summary>
    internal static (string Codigo, string Rotulo)? LerCodigoERotuloDoGrupoAreaEnem(
        LeitorEnvelope leitor,
        JsonObject item,
        string path)
    {
        leitor.ExigirChaves(item, path, "codigo", "rotulo");
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.GrupoAreaEnemCodigo);
        string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.GrupoAreaEnemRotulo);
        return leitor.Falhou ? null : (codigo, rotulo);
    }
}
