namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDaArvoreRestaurada
{
    /// <summary>
    /// Story #923 — as checagens estruturais da árvore de satisfação restaurada,
    /// equivalentes ao que <c>NoExigencia.CriarGrupo</c> já garante na ESCRITA (grupo não
    /// vazio, ciclo, fase comum, cardinalidade por tipo) mais o que só a RAIZ consegue
    /// provar (INV-B6-símile: <c>DocumentoExigidoId</c> de folha existe em
    /// <c>grafo.DocumentosExigidos</c>) e o que só o BANCO garante na escrita normal (Id
    /// único, <c>Ordem</c> única entre raízes e entre irmãos — <c>ux_nos_exigencia_raiz_ordem</c>/
    /// <c>ux_nos_exigencia_irmaos_ordem</c>, <c>NoExigenciaConfiguration</c>). Sem esta
    /// última checagem, um envelope com duas raízes de mesma <c>Ordem</c> passaria pela
    /// decodificação e só falharia no <c>SaveChanges</c>, como <c>23505</c> (unique
    /// violation) — 500 não tratado no meio de uma restauração, em vez de recusa nomeada.
    /// </summary>
    internal static DomainError? Validar(GrafoConfiguracao grafo)
    {
        List<Guid> idsNos = [.. grafo.NosExigencia.Select(n => n.Id)];
        if (idsNos.Distinct().Count() != idsNos.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.IdNoExigenciaDuplicado",
                "O mesmo Id de nó da árvore de satisfação não pode aparecer mais de uma vez na configuração restaurada.");
        }

        List<int> ordensRaizes = [.. grafo.NosExigencia.Where(static n => n.NoPaiId is null).Select(static n => n.Ordem)];
        if (ordensRaizes.Distinct().Count() != ordensRaizes.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemRaizNoExigenciaDuplicada",
                "Cada raiz da árvore de satisfação deve ter uma ordem única dentro do processo.");
        }

        bool irmaosComOrdemDuplicada = grafo.NosExigencia
            .Where(static n => n.NoPaiId is not null)
            .GroupBy(static n => n.NoPaiId)
            .Any(static grupo => grupo.Select(static n => n.Ordem).Distinct().Count() != grupo.Count());
        if (irmaosComOrdemDuplicada)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemIrmaoNoExigenciaDuplicada",
                "Cada filho de um mesmo nó pai deve ter uma ordem única entre os irmãos.");
        }

        // NoExigencia.Reidratar não revalida nada do que NoExigencia.CriarFolha/CriarGrupo
        // garantem na ESCRITA (cardinalidade de folha e de grupo, janela de calendário,
        // consequência, base legal, repetição aninhada). Em vez de duplicar esses
        // invariantes campo a campo, cada raiz é RECONSTRUÍDA aqui pelos MESMOS factories
        // que o cadastro usa — o resultado é descartado (só prova que a árvore SERIA aceita
        // pelo cadastro; a árvore com identidade preservada continua sendo a decodificada
        // por Reidratar, ADR-0110 D2), mas herda de uma vez só todo invariante de
        // construção presente e futuro, sem nunca mais divergir deles.
        if (grafo.NosExigencia.Any(static n => n.Tipo == TipoNo.Folha && n.Filhos.Count > 0))
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaComFilhos",
                "Uma folha da árvore de satisfação restaurada não pode ter filhos — CriarFolha nunca aceita filhos.");
        }

        foreach (NoExigencia raiz in grafo.NosExigencia.Where(static n => n.NoPaiId is null))
        {
            Result<NoExigencia> reconstrucao = ReconstruirNoParaValidarInvariantes(raiz);
            if (reconstrucao.IsFailure)
            {
                return reconstrucao.Error;
            }
        }

        // Folha ↔ DocumentoExigido: EXATAMENTE 1:1 — nem "a mais" (referenciando um
        // DocumentoExigido que não existe no grafo) nem "a menos" (duas folhas para o mesmo
        // DocumentoExigido, que só falharia depois no SaveChanges contra
        // ux_nos_exigencia_documento_exigido_id).
        HashSet<Guid> idsDocumentosExigidos = [.. grafo.DocumentosExigidos.Select(static d => d.Id)];
        List<Guid> documentoIdsReferenciados = [.. grafo.NosExigencia
            .Where(static n => n.Tipo == TipoNo.Folha)
            .Select(static n => n.DocumentoExigidoId!.Value)];

        if (documentoIdsReferenciados.Any(id => !idsDocumentosExigidos.Contains(id)))
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido",
                "Uma folha da árvore de satisfação restaurada referencia um DocumentoExigido que não existe na configuração restaurada.");
        }

        if (documentoIdsReferenciados.Distinct().Count() != documentoIdsReferenciados.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado",
                "Duas folhas da árvore de satisfação restaurada não podem referenciar o mesmo DocumentoExigido.");
        }

        return null;
    }

    /// <summary>
    /// Reconstrói recursivamente um nó (e a subárvore dele) via
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> — ver a nota
    /// em <see cref="Validar"/>. Bottom-up: os filhos são reconstruídos (e
    /// validados) antes do pai, porque <c>CriarGrupo</c> precisa deles para checar grupo
    /// vazio, mesma fase e repetição aninhada.
    /// </summary>
    internal static Result<NoExigencia> ReconstruirNoParaValidarInvariantes(NoExigencia no)
    {
        Result<NoExigencia> resultado;

        if (no.Tipo == TipoNo.Folha)
        {
            resultado = NoExigencia.CriarFolha(
                no.DocumentoExigido!, no.Ordem, no.QuantidadeMinima, no.ChaveDistincao,
                no.DataReferencia, no.OcorrenciasEsperadas, no.RepetePorEntidade);
        }
        else if (no.Tipo is TipoNo.GrupoE or TipoNo.GrupoOu)
        {
            List<NoExigencia> filhosReconstruidos = new(no.Filhos.Count);
            foreach (NoExigencia filho in no.Filhos)
            {
                Result<NoExigencia> filhoResultado = ReconstruirNoParaValidarInvariantes(filho);
                if (filhoResultado.IsFailure)
                {
                    return filhoResultado;
                }

                filhosReconstruidos.Add(filhoResultado.Value!);
            }

            List<NoExigenciaBaseLegal> basesLegaisReconstruidas = new(no.BasesLegais.Count);
            foreach (NoExigenciaBaseLegal baseLegal in no.BasesLegais)
            {
                Result<NoExigenciaBaseLegal> baseLegalResultado = NoExigenciaBaseLegal.Criar(
                    baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Status, baseLegal.Observacao);
                if (baseLegalResultado.IsFailure)
                {
                    return Result<NoExigencia>.Failure(baseLegalResultado.Error!);
                }

                basesLegaisReconstruidas.Add(baseLegalResultado.Value!);
            }

            resultado = NoExigencia.CriarGrupo(
                no.Tipo, no.Ordem, no.QuantidadeMinima, no.Consequencia, basesLegaisReconstruidas,
                filhosReconstruidos, no.RepetePorEntidade);
        }
        else
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaTipoInvalido",
                $"O nó {no.Id} da árvore de satisfação restaurada tem um tipo desconhecido."));
        }

        return resultado.IsFailure ? resultado : ValidarCanonicidade(no, resultado.Value!);
    }

    /// <summary>
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> não só
    /// VALIDAM — alguns campos são NORMALIZADOS silenciosamente (<c>quantidadeMinima</c>
    /// nula vira <see cref="NoExigencia.QuantidadeMinimaPadrao"/>; consequência em branco
    /// vira <see langword="null"/>). A reconstrução por si só SUCEDE mesmo quando o nó
    /// DECODIFICADO tinha o valor não-canônico — e é o nó DECODIFICADO, não o reconstruído,
    /// que é aplicado ao agregado (ADR-0110 D2). Sem esta comparação, uma folha ou grupo
    /// OU/N-de com <c>quantidadeMinima: null</c> passaria por aqui e só falharia depois, no
    /// <c>SaveChanges</c>, contra <c>ck_nos_exigencia_tipo_campos_coerentes</c>.
    /// </summary>
    internal static Result<NoExigencia> ValidarCanonicidade(NoExigencia decodificado, NoExigencia reconstruido)
    {
        if (reconstruido.QuantidadeMinima != decodificado.QuantidadeMinima)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada não declara quantidadeMinima — um encoder real nunca a omite em folha ou grupo OU/N-de."));
        }

        if (reconstruido.Consequencia != decodificado.Consequencia)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaConsequenciaNaoCanonica",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada declara uma consequência que não está na forma canônica."));
        }

        // CriarFolha e CriarGrupo não compartilham todos os parâmetros — cada um aceita só
        // os campos do seu próprio tipo (chaveDistincao/dataReferencia/ocorrenciasEsperadas
        // são exclusivos de folha; basesLegais própria é exclusiva de grupo). A reconstrução
        // de um nó com o tipo errado desses campos simplesmente os IGNORA (nem chegam a ser
        // parâmetro da factory) em vez de recusar, e a comparação acima não pega isso. Um nó
        // decodificado carregando o campo do outro tipo violaria
        // ck_nos_exigencia_tipo_campos_coerentes no SaveChanges (folha/grupo) ou persistiria
        // uma base legal em folha que a API de escrita normal nunca produz.
        if (decodificado.Tipo == TipoNo.Folha)
        {
            if (decodificado.BasesLegais.Count > 0)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "ProcessoSeletivo.NoExigenciaFolhaComBaseLegal",
                    $"O nó {decodificado.Id} da árvore de satisfação restaurada é uma folha, mas carrega base legal — campo exclusivo de grupo OU/N-de."));
            }
        }
        else if (decodificado.ChaveDistincao is not null
            || decodificado.DataReferencia is not null
            || decodificado.OcorrenciasEsperadas is not null)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaGrupoComCampoDeFolha",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada é um grupo, mas carrega chaveDistincao, dataReferencia ou ocorrenciasEsperadas — campos exclusivos de folha."));
        }

        return Result<NoExigencia>.Success(reconstruido);
    }
}
