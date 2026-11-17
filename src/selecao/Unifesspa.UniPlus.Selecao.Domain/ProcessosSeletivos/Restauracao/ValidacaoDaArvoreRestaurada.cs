namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As checagens estruturais da árvore de satisfação restaurada, equivalentes ao que <c>NoExigencia.CriarGrupo</c> já garante
/// na gravação (grupo não vazio, ciclo, fase comum, cardinalidade por tipo), mais o que só a raiz consegue provar
/// (<c>DocumentoExigidoId</c> de folha existe em <c>grafo.DocumentosExigidos</c>) e o que só o banco garante na gravação
/// normal (Id único, <c>Ordem</c> única entre raízes e entre irmãos, em <c>ux_nos_da_arvore_de_exigencias_raiz_ordem</c> e
/// <c>ux_nos_da_arvore_de_exigencias_irmaos_ordem</c>). Sem esta última checagem, um envelope com duas raízes de mesma
/// <c>Ordem</c> passaria pela decodificação e só falharia no <c>SaveChanges</c>, como violação de unicidade (23505): erro
/// não tratado no meio de uma restauração, em vez de recusa nomeada.
/// </summary>
internal static class ValidacaoDaArvoreRestaurada
{
    private const string Campo = "documentosExigidos";

    /// <summary>
    /// Cada família confere o que lhe cabe e todas acumulam. O que depende de dado já recusado não se avalia: a reconstrução pula a
    /// raiz cuja subárvore tem nó de Id repetido, folha com filhos ou folha sem documento, porque o factory que a reconstrói lê
    /// exatamente isso, e a leitura dos documentos das folhas só conta as folhas que declaram documento.
    /// </remarks>
    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo)
    {
        IReadOnlySet<NoExigencia> recusados = NosRecusados(grafo);

        return
        [
            .. CausasDaRecusa.Da(IdDuplicado(grafo), Campo),
            .. CausasDaRecusa.Da(OrdemDasRaizesDuplicada(grafo), Campo),
            .. CausasDaRecusa.Da(OrdemDosIrmaosDuplicada(grafo), Campo),
            .. CausasDaRecusa.Da(FolhaComFilhos(grafo), Campo),
            .. ReconstrucaoDasRaizes(grafo, recusados),
            .. CausasDaRecusa.Da(FolhaSemDocumentoExigido(grafo), Campo),
            .. CausasDaRecusa.Da(DocumentoExigidoEmMaisDeUmaFolha(grafo), Campo),
        ];
    }

    private static HashSet<NoExigencia> NosRecusados(GrafoConfiguracao grafo)
    {
        HashSet<Guid> documentos = [.. grafo.DocumentosExigidos.Select(static d => d.Id)];
        HashSet<Guid> idsRepetidos = [.. grafo.NosExigencia.GroupBy(static n => n.Id).Where(static g => g.Count() > 1).Select(static g => g.Key)];

        return new(
            grafo.NosExigencia.Where(no => idsRepetidos.Contains(no.Id) || FolhaComFilhos(no) || FolhaSemDocumento(no, documentos)),
            ReferenceEqualityComparer.Instance);
    }

    private static bool FolhaComFilhos(NoExigencia no) => no.Tipo == TipoNo.Folha && no.Filhos.Count > 0;

    private static bool FolhaSemDocumento(NoExigencia no, HashSet<Guid> documentos) =>
        no.Tipo == TipoNo.Folha && (no.DocumentoExigidoId is not { } id || no.DocumentoExigido is null || !documentos.Contains(id));

    private static bool TemNoRecusado(NoExigencia no, IReadOnlySet<NoExigencia> recusados) =>
        recusados.Contains(no) || no.Filhos.Any(filho => TemNoRecusado(filho, recusados));

    private static DomainError? IdDuplicado(GrafoConfiguracao grafo) =>
        grafo.NosExigencia.Select(static n => n.Id).Distinct().Count() != grafo.NosExigencia.Count
            ? new DomainError(
                "ProcessoSeletivo.IdNoExigenciaDuplicado",
                "O mesmo Id de nó da árvore de satisfação não pode aparecer mais de uma vez na configuração restaurada.")
            : null;

    private static DomainError? OrdemDasRaizesDuplicada(GrafoConfiguracao grafo)
    {
        List<int> ordens = [.. grafo.NosExigencia.Where(static n => n.NoPaiId is null).Select(static n => n.Ordem)];

        return ordens.Distinct().Count() != ordens.Count
            ? new DomainError(
                "ProcessoSeletivo.OrdemRaizNoExigenciaDuplicada",
                "Cada raiz da árvore de satisfação deve ter uma ordem única dentro do processo.")
            : null;
    }

    private static DomainError? OrdemDosIrmaosDuplicada(GrafoConfiguracao grafo) =>
        grafo.NosExigencia
            .Where(static n => n.NoPaiId is not null)
            .GroupBy(static n => n.NoPaiId)
            .Any(static grupo => grupo.Select(static n => n.Ordem).Distinct().Count() != grupo.Count())
            ? new DomainError(
                "ProcessoSeletivo.OrdemIrmaoNoExigenciaDuplicada",
                "Cada filho de um mesmo nó pai deve ter uma ordem única entre os irmãos.")
            : null;

    private static DomainError? FolhaComFilhos(GrafoConfiguracao grafo) =>
        grafo.NosExigencia.Any(FolhaComFilhos)
            ? new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaComFilhos",
                "Uma folha da árvore de satisfação restaurada não pode ter filhos — CriarFolha nunca aceita filhos.")
            : null;

    // NoExigencia.Reidratar não revalida nada do que NoExigencia.CriarFolha e CriarGrupo garantem na gravação (cardinalidade
    // de folha e de grupo, janela de calendário, consequência, base legal, repetição aninhada). Em vez de duplicar esses
    // invariantes campo a campo, cada raiz é reconstruída pelos mesmos factories que o cadastro usa: o resultado é descartado
    // (só prova que a árvore seria aceita pelo cadastro; a árvore com identidade preservada continua sendo a decodificada por
    // Reidratar, ADR-0110), mas herda de uma vez só todo invariante de construção presente e futuro.
    private static IEnumerable<FieldError> ReconstrucaoDasRaizes(GrafoConfiguracao grafo, IReadOnlySet<NoExigencia> recusados) =>
        grafo.NosExigencia
            .Where(n => n.NoPaiId is null && !TemNoRecusado(n, recusados))
            .Select(ReconstruirNoParaValidarInvariantes)
            .Where(static reconstrucao => reconstrucao.IsFailure)
            .Select(static reconstrucao => new FieldError(Campo, reconstrucao.Error!));

    // Folha e DocumentoExigido são 1:1: uma folha que cita documento inexistente no grafo, ou duas folhas para o mesmo
    // documento, só falhariam depois no SaveChanges, contra ux_nos_da_arvore_de_exigencias_documento_exigido_id.
    private static DomainError? FolhaSemDocumentoExigido(GrafoConfiguracao grafo)
    {
        HashSet<Guid> existentes = [.. grafo.DocumentosExigidos.Select(static d => d.Id)];

        return grafo.NosExigencia.Any(no => FolhaSemDocumento(no, existentes))
            ? new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido",
                "Uma folha da árvore de satisfação restaurada referencia um DocumentoExigido que não existe na configuração restaurada.")
            : null;
    }

    private static DomainError? DocumentoExigidoEmMaisDeUmaFolha(GrafoConfiguracao grafo) =>
        DocumentosDasFolhas(grafo).Distinct().Count() != DocumentosDasFolhas(grafo).Count
            ? new DomainError(
                "ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado",
                "Duas folhas da árvore de satisfação restaurada não podem referenciar o mesmo DocumentoExigido.")
            : null;

    private static List<Guid> DocumentosDasFolhas(GrafoConfiguracao grafo) =>
        [.. grafo.NosExigencia.Where(static n => n.Tipo == TipoNo.Folha).Where(static n => n.DocumentoExigidoId.HasValue).Select(static n => n.DocumentoExigidoId!.Value)];

    /// <summary>
    /// Reconstrói recursivamente um nó (e a subárvore dele) via
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> — ver a nota
    /// em <see cref="Validar"/>. Bottom-up: os filhos são reconstruídos (e
    /// validados) antes do pai, porque <c>CriarGrupo</c> precisa deles para checar grupo
    /// vazio, mesma fase e repetição aninhada.
    /// </summary>
    internal static Result<NoExigencia> ReconstruirNoParaValidarInvariantes(NoExigencia no) =>
        no.Tipo switch
        {
            TipoNo.Folha => NaFormaCanonica(no, ReconstruirFolha(no)),
            TipoNo.GrupoE or TipoNo.GrupoOu => NaFormaCanonica(no, ReconstruirGrupo(no)),
            _ => Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaTipoInvalido",
                $"O nó {no.Id} da árvore de satisfação restaurada tem um tipo desconhecido.")),
        };

    private static Result<NoExigencia> NaFormaCanonica(NoExigencia decodificado, Result<NoExigencia> reconstruido) =>
        reconstruido.IsFailure ? reconstruido : ValidarCanonicidade(decodificado, reconstruido.Value!);

    private static Result<NoExigencia> ReconstruirFolha(NoExigencia no) =>
        no.DocumentoExigido is null
            ? Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido",
                $"A folha {no.Id} da árvore de satisfação restaurada não tem documento exigido."))
            : NoExigencia.CriarFolha(
                no.DocumentoExigido, no.Ordem, no.QuantidadeMinima, no.ChaveDistincao,
                no.DataReferencia, no.OcorrenciasEsperadas, no.RepetePorEntidade);

    private static Result<NoExigencia> ReconstruirGrupo(NoExigencia no)
    {
        Result<List<NoExigencia>> filhos = ReconstruirFilhos(no);
        Result<List<NoExigenciaBaseLegal>> basesLegais = ReconstruirBasesLegais(no);

        if (filhos.IsFailure)
        {
            return Result<NoExigencia>.Failure(filhos.Error!);
        }

        return basesLegais.IsFailure
            ? Result<NoExigencia>.Failure(basesLegais.Error!)
            : NoExigencia.CriarGrupo(
                no.Tipo, no.Ordem, no.QuantidadeMinima, no.Consequencia, basesLegais.Value!, filhos.Value!, no.RepetePorEntidade);
    }

    private static Result<List<NoExigencia>> ReconstruirFilhos(NoExigencia no)
    {
        List<NoExigencia> reconstruidos = new(no.Filhos.Count);
        foreach (NoExigencia filho in no.Filhos)
        {
            Result<NoExigencia> resultado = ReconstruirNoParaValidarInvariantes(filho);
            if (resultado.IsFailure)
            {
                return Result<List<NoExigencia>>.Failure(resultado.Error!);
            }

            reconstruidos.Add(resultado.Value!);
        }

        return Result<List<NoExigencia>>.Success(reconstruidos);
    }

    private static Result<List<NoExigenciaBaseLegal>> ReconstruirBasesLegais(NoExigencia no)
    {
        List<NoExigenciaBaseLegal> reconstruidas = new(no.BasesLegais.Count);
        foreach (NoExigenciaBaseLegal baseLegal in no.BasesLegais)
        {
            Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
                baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Status, baseLegal.Observacao);
            if (resultado.IsFailure)
            {
                return Result<List<NoExigenciaBaseLegal>>.Failure(resultado.Error!);
            }

            reconstruidas.Add(resultado.Value!);
        }

        return Result<List<NoExigenciaBaseLegal>>.Success(reconstruidas);
    }

    /// <summary>
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> não só
    /// VALIDAM — alguns campos são NORMALIZADOS silenciosamente (<c>quantidadeMinima</c>
    /// nula vira <see cref="NoExigencia.QuantidadeMinimaPadrao"/>; consequência em branco
    /// vira <see langword="null"/>). A reconstrução por si só SUCEDE mesmo quando o nó
    /// DECODIFICADO tinha o valor não-canônico — e é o nó DECODIFICADO, não o reconstruído,
    /// que é aplicado ao agregado (ADR-0110 D2). Sem esta comparação, uma folha ou grupo
    /// OU/N-de com <c>quantidadeMinima: null</c> passaria por aqui e só falharia depois, no
    /// <c>SaveChanges</c>, contra <c>ck_nos_da_arvore_de_exigencias_tipo_campos_coerentes</c>.
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
        // ck_nos_da_arvore_de_exigencias_tipo_campos_coerentes no SaveChanges (folha/grupo) ou persistiria
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
