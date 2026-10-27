namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// SEM <c>[NonTransactional]</c>, de propósito (issue #1071): este handler depende da
    /// transação ambiente do Wolverine para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>
    /// — o <c>SELECT ... FOR UPDATE</c> só serializa handlers concorrentes porque roda
    /// enrolado nela (ver comentário em <c>ProcessoSeletivoRepository.ObterParaMutacaoAsync</c>);
    /// <c>[NonTransactional]</c> desabilitaria esse enrolamento e o lock pessimista deixaria
    /// de bloquear qualquer coisa. <see cref="PublicarProcessoSeletivoCommandHandler"/> já prova
    /// que injetar reader cross-módulo (aqui, <see cref="ITipoEtapaReader"/> sobre o cadastro de
    /// Configuração e <see cref="ITipoAtoPublicadoReader"/> sobre o catálogo de Publicações) junto
    /// do mesmo lock não é ambíguo para o <c>AutoApplyTransactions</c> — só um handler que injeta
    /// diretamente um <em>segundo DbContext concreto</em> (não um reader por trás de interface
    /// pública) precisaria do opt-in. O que mantém o segundo DbContext fora da árvore que o
    /// codegen enxerga é o <c>AlwaysUseServiceLocationFor</c> declarado para cada um desses
    /// contratos: um reader novo sem esse opt-in reintroduz a ambiguidade de DbContext e derruba
    /// o enrolamento transacional de que o lock depende.
    /// </summary>
    /// <remarks>
    /// Em duas passadas (ADR-0125): a primeira acumula a forma de TODAS as etapas via
    /// <see cref="EtapaProcesso.ValidarFormaBasica"/>, sem tocar o <see cref="ITipoEtapaReader"/>;
    /// só se ela vier limpa a segunda resolve cada tipo de etapa no cadastro e chama
    /// <see cref="EtapaProcesso.Criar"/>/<see cref="EtapaProcesso.AtualizarDados"/>, que
    /// reinvocam a mesma checagem como defesa em profundidade.
    /// </remarks>
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoEtapaReader tipoEtapaReader,
        ITipoBancaReader tipoBancaReader,
        IRegraCatalogoReader regraCatalogoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(tipoBancaReader);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(tipoAtoPublicadoReader);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou — inclusive antes da
        // resolução de TipoEtapaOrigemId contra o cadastro de Configuração, que só roda a
        // seguir.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<Guid> idsInformados = [.. command.Etapas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value)];
        if (idsInformados.Distinct().Count() != idsInformados.Count)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode ser informado mais de uma vez no mesmo payload."));
        }

        // Acumula (ADR-0125) a forma de TODAS as etapas do payload numa primeira passada, com
        // EtapaProcesso.ValidarFormaBasica — nenhuma dessas checagens depende do cadastro de
        // tipos de etapa. Só roda antes do tipoEtapaReader (validação vence I/O dentro do
        // bucket 422, PR #1211): sem isso, um payload com 2 etapas — uma com nome vazio, outra
        // com TipoEtapaOrigemId de um tipo inativo — devolveria só o erro de catálogo da
        // segunda etapa, escondendo a falha de forma da primeira.
        List<FieldError> formaErros = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput itemForma = command.Etapas[indice];
            formaErros.AddRange(ComIndice(
                EtapaProcesso.ValidarFormaBasica(
                    itemForma.Nome, itemForma.Carater, itemForma.Peso, itemForma.NotaMinima, itemForma.Ordem,
                    itemForma.NotaMaxima, itemForma.PassoDoLancamento),
                indice));
        }

        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
        }

        // Reconcilia por Id em vez de recriar toda a coleção: uma etapa cujo
        // Id (ecoado pelo cliente a partir da leitura anterior) ainda existe
        // no processo é ATUALIZADA na mesma instância tracked, preservando o
        // etapa_ref que critérios de desempate/eliminação da classificação
        // possam ter — do contrário, todo PUT /etapas geraria etapas com Id
        // novo e invalidaria essas referências por construção.
        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);

        // O snapshot inteiro é resolvido contra o cadastro corrente SÓ quando o vínculo é novo
        // ou muda de tipo — nunca para uma etapa existente cujo TipoEtapaOrigemId não mudou,
        // que no máximo tem os sinalizadores regravados (abaixo). Sem essa distinção,
        // (a) desativar um tipo já vinculado bloquearia QUALQUER PUT subsequente da coleção
        // inteira, mesmo editando só o Peso de uma etapa não relacionada; e (b) renomear um
        // tipo ainda ativo reescreveria silenciosamente o snapshot já congelado de uma etapa
        // que o cliente nem tocou — violando o próprio propósito do snapshot-copy (ADR-0061):
        // a identidade congelada só muda quando o vínculo muda, não quando o cadastro de origem
        // muda. Os sinalizadores do tipo mudam também quando o caráter DESTA etapa muda, porque
        // é quando a gravação já relê o tipo para conferi-lo (passada abaixo).
        //
        // O cache guarda o TipoEtapaView (dado bruto do catálogo, evita reconsultar o reader
        // quando duas etapas NOVAS ou realinhadas compartilham o mesmo tipo), NUNCA a instância
        // de TipoEtapaSnapshot já construída — TipoEtapa é owned type (OwnsOne) de EtapaProcesso,
        // e EF Core não aceita a MESMA instância pertencendo a dois donos ao mesmo tempo; duas
        // etapas com o mesmo tipo, cada uma com sua própria instância, é caso legítimo e comum.
        Dictionary<Guid, TipoEtapaView> tiposEmCache = [];

        // O caráter declarado é conferido contra o que o tipo admite — e numa passada própria,
        // ANTES de qualquer mutação: assim um payload com duas etapas incoerentes devolve as
        // duas (ADR-0125), e nenhuma instância tracked precisa ser descartada.
        //
        // A passada só lê o cadastro quando há o que conferir: etapa existente que mantém o
        // vínculo E o caráter não é reavaliada. Sem esse recorte, estreitar um tipo ainda ATIVO
        // — marcar que ele deixou de compor a nota final, que é justamente a operação que este
        // cadastro existe para permitir — passaria a recusar todo PUT posterior de qualquer
        // certame que já tivesse uma etapa daquele tipo, mesmo editando só o nome de outra.
        //
        // Quando a etapa existente muda de caráter e o cadastro é lido, o que foi lido é também
        // o que ela passa a congelar: os sinalizadores são regravados no snapshot DESTA etapa.
        // O registro é por posição no payload, nunca por tipo — o cache é compartilhado, e
        // editar uma etapa não pode refrescar o snapshot de outra do mesmo tipo.
        Dictionary<int, TipoEtapaView> sinalizadoresRelidos = [];
        List<FieldError> caraterErros = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput itemCarater = command.Etapas[indice];
            EtapaProcesso? jaExistente = itemCarater.Id is { } idExistente
                && existentes.TryGetValue(idExistente, out EtapaProcesso? emCurso) ? emCurso : null;
            bool vinculoInalterado = jaExistente is not null
                && jaExistente.TipoEtapaOrigemId == itemCarater.TipoEtapaOrigemId;
            if (vinculoInalterado && jaExistente!.Carater == itemCarater.Carater)
            {
                continue;
            }

            if (!tiposEmCache.TryGetValue(itemCarater.TipoEtapaOrigemId, out TipoEtapaView? tipoParaConferir))
            {
                tipoParaConferir = await tipoEtapaReader
                    .ObterAtivoPorIdAsync(itemCarater.TipoEtapaOrigemId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoParaConferir is null)
                {
                    // Vínculo inalterado e tipo desde então desativado: recusar puniria quem só
                    // quer corrigir o caráter de uma etapa que já existia, mas o que o tipo
                    // admitia continua conhecido — está congelado no snapshot da etapa, e é
                    // contra ele que o caráter novo é conferido.
                    if (vinculoInalterado)
                    {
                        TipoEtapaSnapshot congelado = jaExistente!.TipoEtapa;
                        caraterErros.AddRange(ErrosDeCarater(
                            itemCarater.Carater, congelado.AdmitePontuacao, congelado.AdmiteEliminacao, congelado.Nome, indice));
                        continue;
                    }

                    // Acumula com as recusas de caráter em vez de sair na primeira: quem
                    // errou o tipo de uma etapa e o caráter de outra corrige as duas de uma
                    // vez, e não descobre a segunda só na tentativa seguinte.
                    caraterErros.Add(new($"etapas[{indice}].tipoEtapaOrigemId", new DomainError(
                        "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
                        $"Tipo de etapa {itemCarater.TipoEtapaOrigemId} não encontrado ou não está ativo.")));
                    continue;
                }

                tiposEmCache[itemCarater.TipoEtapaOrigemId] = tipoParaConferir;
            }

            caraterErros.AddRange(ErrosDeCarater(
                itemCarater.Carater, tipoParaConferir.AdmitePontuacao, tipoParaConferir.AdmiteEliminacao, tipoParaConferir.Nome, indice));

            if (vinculoInalterado)
            {
                sinalizadoresRelidos[indice] = tipoParaConferir;
            }
        }

        if (caraterErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(caraterErros);
        }

        Dictionary<(string Codigo, string Versao), RegraCatalogo?> regrasResolvidas = [];

        async Task<RegraCatalogo?> ResolverRegraAsync(string codigo, string versao)
        {
            if (regrasResolvidas.TryGetValue((codigo, versao), out RegraCatalogo? memoizada))
            {
                return memoizada;
            }

            RegraCatalogo? resolvida = await regraCatalogoReader
                .ObterAsync(codigo, versao, cancellationToken)
                .ConfigureAwait(false);
            regrasResolvidas[(codigo, versao)] = resolvida;
            return resolvida;
        }

        // Se a nota da etapa vem do ENEM: o snapshot congelado quando o vínculo com o tipo não mudou,
        // e o tipo lido na passada do caráter quando é vínculo novo ou alterado.
        bool NotaDeOrigemNoEnemDe(EtapaProcessoInput item) =>
            item.Id is { } id
            && existentes.TryGetValue(id, out EtapaProcesso? atual)
            && atual.TipoEtapaOrigemId == item.TipoEtapaOrigemId
                ? atual.TipoEtapa.NotaDeOrigemNoEnem
                : tiposEmCache[item.TipoEtapaOrigemId].NotaDeOrigemNoEnem;

        // A forma de avaliação e a regra de consolidação são resolvidas numa passada própria,
        // ANTES de qualquer mutação, como o caráter: a forma é um token do contrato, e o token
        // desconhecido é RECUSADO em vez de lido como ausente (um erro de digitação viraria etapa
        // sem forma, e só o gate de publicação o acusaria, longe de quem o cometeu); a regra é
        // montada a partir dos valores RESOLVIDOS do catálogo, nunca ecoados do payload, para o
        // hash bater por construção. As coerências que independem do catálogo rodam para TODAS
        // as etapas antes de qualquer leitura dele, e acumulam entre as etapas (ADR-0125).
        Dictionary<int, FormaDeAvaliacao?> formasDeAvaliacao = [];
        Dictionary<int, (string Codigo, string Versao)> regrasDeclaradas = [];
        List<FieldError> avaliacaoErros = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput itemAvaliacao = command.Etapas[indice];
            List<FieldError> errosDaEtapa = [];

            if (FormaDeAvaliacaoCodigo.TentarConverter(itemAvaliacao.FormaDeAvaliacao, out FormaDeAvaliacao? forma))
            {
                formasDeAvaliacao[indice] = forma;
            }
            else
            {
                errosDaEtapa.Add(new("formaDeAvaliacao", new DomainError(
                    EtapaProcesso.FormaDeAvaliacaoDesconhecida,
                    $"A forma de avaliação '{itemAvaliacao.FormaDeAvaliacao}', declarada pela etapa '{itemAvaliacao.Nome}', não existe — use '{FormaDeAvaliacaoCodigo.Objetiva}' ou '{FormaDeAvaliacaoCodigo.Humana}'.")));
            }

            string? codigo = itemAvaliacao.RegraConsolidacaoCodigo?.Trim();
            string? versao = itemAvaliacao.RegraConsolidacaoVersao?.Trim();
            bool temCodigo = !string.IsNullOrEmpty(codigo);
            bool temVersao = !string.IsNullOrEmpty(versao);
            if (temCodigo != temVersao)
            {
                errosDaEtapa.Add(new("regraConsolidacao", new DomainError(
                    EtapaProcesso.RegraDeConsolidacaoInvalida,
                    $"A regra de consolidação da etapa '{itemAvaliacao.Nome}' precisa de código e versão juntos: falta {(temCodigo ? "a versão" : "o código")}.")));
            }

            // Só com a forma já entendida: forma desconhecida não vira também "regra fora da forma humana".
            if (formasDeAvaliacao.ContainsKey(indice))
            {
                errosDaEtapa.AddRange(EtapaProcesso.ValidarAvaliacao(
                    NotaDeOrigemNoEnemDe(itemAvaliacao),
                    itemAvaliacao.PassoDoLancamento,
                    forma,
                    temCodigo && temVersao ? codigo : null));
            }

            avaliacaoErros.AddRange(ComIndice(errosDaEtapa, indice));

            if (errosDaEtapa.Count == 0 && temCodigo && temVersao)
            {
                regrasDeclaradas[indice] = (codigo!, versao!);
            }
        }

        if (avaliacaoErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(avaliacaoErros);
        }

        Dictionary<int, ReferenciaRegra> regrasDeConsolidacao = [];
        foreach ((int indice, (string codigo, string versao)) in regrasDeclaradas)
        {
            EtapaProcessoInput itemAvaliacao = command.Etapas[indice];
            RegraCatalogo? regraDeConsolidacao = await ResolverRegraAsync(codigo, versao).ConfigureAwait(false);
            if (regraDeConsolidacao is null || regraDeConsolidacao.Tipo != TipoRegra.RegraConsolidacao)
            {
                avaliacaoErros.Add(new($"etapas[{indice}].regraConsolidacao", new DomainError(
                    EtapaProcesso.RegraDeConsolidacaoInvalida,
                    $"A regra {codigo}/{versao}, declarada pela etapa '{itemAvaliacao.Nome}', não é uma regra de consolidação do catálogo.")));
                continue;
            }

            Result<ReferenciaRegra> consolidacaoResult = ReferenciaRegra.Criar(
                regraDeConsolidacao.Codigo, regraDeConsolidacao.Versao, regraDeConsolidacao.Hash);
            if (consolidacaoResult.IsFailure)
            {
                avaliacaoErros.AddRange(ComIndice(
                    consolidacaoResult.Errors.Select(erro => new FieldError("regraConsolidacao", erro.Error)), indice));
                continue;
            }

            regrasDeConsolidacao[indice] = consolidacaoResult.Value!;
        }

        if (avaliacaoErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(avaliacaoErros);
        }

        List<EtapaProcesso> etapas = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput input = command.Etapas[indice];
            EtapaProcesso? etapaExistente = input.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? candidata)
                ? candidata
                : null;

            TipoEtapaSnapshot tipoEtapa;
            if (etapaExistente is not null && etapaExistente.TipoEtapaOrigemId == input.TipoEtapaOrigemId)
            {
                tipoEtapa = sinalizadoresRelidos.TryGetValue(indice, out TipoEtapaView? relido)
                    ? etapaExistente.TipoEtapa.ComSinalizadores(relido.AdmitePontuacao, relido.AdmiteEliminacao)
                    : etapaExistente.TipoEtapa;
            }
            else
            {
                // Vínculo novo ou alterado já foi resolvido e cacheado pela passada que confere
                // o caráter, e é lá que o tipo inativo é recusado — antes de qualquer mutação.
                TipoEtapaView tipo = tiposEmCache[input.TipoEtapaOrigemId];

                Result<TipoEtapaSnapshot> snapshotResult = TipoEtapaSnapshot.Criar(
                    tipo.Id, tipo.Codigo, tipo.Nome, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem);
                if (snapshotResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(snapshotResult.Error!);
                }

                tipoEtapa = snapshotResult.Value!;
            }

            if (etapaExistente is not null)
            {
                // Defesa em profundidade: ValidarFormaBasica já rodou limpa na primeira
                // passada acima — esta falha só ocorreria por dessincronia entre as duas
                // checagens, nunca pelo payload em si.
                Result atualizarResult = etapaExistente.AtualizarDados(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem,
                    input.FaseCodigo, input.NotaMaxima, input.PassoDoLancamento,
                    formasDeAvaliacao[indice], regrasDeConsolidacao.GetValueOrDefault(indice));
                if (atualizarResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(ComIndice(atualizarResult.Errors, indice));
                }

                etapas.Add(etapaExistente);
            }
            else
            {
                Result<EtapaProcesso> criarResult = EtapaProcesso.Criar(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem,
                    input.FaseCodigo, input.NotaMaxima, input.PassoDoLancamento,
                    formasDeAvaliacao[indice], regrasDeConsolidacao.GetValueOrDefault(indice));
                if (criarResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(ComIndice(criarResult.Errors, indice));
                }

                etapas.Add(criarResult.Value!);
            }
        }

        // UMA leitura do relógio para toda a operação (ADR-0068): todo produto declarado, de
        // qualquer etapa da coleção, resolve a vigência contra o MESMO instante. Duas leituras
        // deixariam a coleção atravessar a virada do dia e aceitar metade dela.
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Memoiza a resolução por código dentro da execução do comando, como o cronograma de
        // fases faz um nível acima. A etapa publica o mesmo ato duas vezes — preliminar e
        // definitivo —, e o número de produtos cresce com a coleção inteira: sem isto, cada item
        // de cada etapa releria o catálogo, e as idas ao banco acontecem com o SELECT ... FOR
        // UPDATE do certame na mão, serializando quem edita o mesmo certame por todo esse tempo.
        Dictionary<string, TipoAtoPublicadoView?> tiposDeAtoResolvidos = new(StringComparer.Ordinal);

        async Task<TipoAtoPublicadoView?> ResolverTipoDeAtoAsync(string codigo)
        {
            if (tiposDeAtoResolvidos.TryGetValue(codigo, out TipoAtoPublicadoView? memoizado))
            {
                return memoizado;
            }

            TipoAtoPublicadoView? resolvido = await tipoAtoPublicadoReader
                .ObterVigenteAsync(codigo, hoje, cancellationToken)
                .ConfigureAwait(false);
            tiposDeAtoResolvidos[codigo] = resolvido;
            return resolvido;
        }

        // O tipo que a versão publicada já declara vale pela classificação congelada nela, como na
        // retificação; o catálogo de hoje só vale para o tipo declarado pela primeira vez.
        ClassificacaoDosTiposNaEdicao congelados = new(processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId);

        // Mesma memoização, e pela mesma razão: o tipo de banca e a regra de prazo são os dois
        // outros cadastros que este handler relê por ITEM de CADA etapa. Duas etapas que
        // requerem a mesma banca, ou que abrem janela recursal sob a mesma regra, são o caso
        // comum — não a exceção —, e cada releitura acontece com o SELECT ... FOR UPDATE do
        // certame na mão. O TipoBancaView e o RegraCatalogo memoizados são dado bruto do
        // cadastro: BancaDaEtapa.Criar e ReferenciaRegra.Criar continuam construindo uma
        // instância própria por etapa, como o snapshot do tipo de etapa já exige.
        Dictionary<Guid, TipoBancaView?> tiposDeBancaResolvidos = [];

        async Task<TipoBancaView?> ResolverTipoDeBancaAsync(Guid tipoBancaId)
        {
            if (tiposDeBancaResolvidos.TryGetValue(tipoBancaId, out TipoBancaView? memoizado))
            {
                return memoizado;
            }

            TipoBancaView? resolvido = await tipoBancaReader
                .ObterPorIdAsync(tipoBancaId, cancellationToken)
                .ConfigureAwait(false);
            tiposDeBancaResolvidos[tipoBancaId] = resolvido;
            return resolvido;
        }

        // Os produtos são declarados por etapa, depois que a etapa existe: é ela quem os
        // vincula, e a recusa de ato repetido é dela.
        for (int i = 0; i < etapas.Count; i++)
        {
            EtapaProcessoInput input = command.Etapas[i];

            // As âncoras que a etapa já tinha antes desta gravação: o cliente devolve o que leu, e uma
            // âncora que já estava no agregado não é revista pelo que o catálogo diz hoje do ato.
            HashSet<string> atosJaAncorados = [.. etapas[i].Recursos
                .SelectMany(recurso => etapas[i].Produtos
                    .Where(p => p.Id == recurso.ProdutoAncoraId && p.Papel == PapelProdutoFase.Preliminar)
                    .Select(static p => p.AtoCodigo))];

            // O papel desconhecido é RECUSADO, e não tratado como ausente. Ignorar a conversão
            // fazia um erro de digitação — `PRELIMINARR` por `PRELIMINAR` — virar produto sem
            // papel: a etapa deixava de publicar resultado, o recurso perdia onde ancorar, e
            // nada dizia isso a quem declarou. Mesma recusa que a fase dá um nível acima.
            List<ProdutoDaEtapa> produtosDaEtapa = [];
            foreach (ProdutoDaEtapaInput d in input.Produtos)
            {
                if (!PapelProdutoFaseCodigo.TentarConverter(d.Papel, out PapelProdutoFase? papel))
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProdutoDaEtapa.PapelDesconhecido",
                        $"O papel '{d.Papel}', declarado pela etapa '{input.Nome}', não é declarável — use '{PapelProdutoFaseCodigo.Preliminar}', '{PapelProdutoFaseCodigo.Definitivo}' ou nenhum."));
                }

                // Conferido contra o catálogo SÓ quando a etapa passa a declarar este par
                // (ato, papel) — nunca para um que ela já declarava. É a mesma distinção que o
                // tipo da etapa recebe acima, e pela mesma razão: aposentar no catálogo um ato
                // já declarado bloquearia QUALQUER PUT posterior da coleção inteira, porque o
                // endpoint substitui tudo e o cliente devolve o produto que leu. Quem só quis
                // corrigir o nome de outra etapa ficaria preso, sem nada na tela dizendo que o
                // caminho de saída é apagar um produto que ele não declarou hoje.
                bool jaDeclarado = etapas[i].Produtos.Any(p =>
                    string.Equals(p.AtoCodigo, d.AtoCodigo, StringComparison.Ordinal) && p.Papel == papel);
                if (jaDeclarado)
                {
                    produtosDaEtapa.Add(ProdutoDaEtapa.Criar(d.AtoCodigo, papel));
                    continue;
                }

                // O ato precisa existir no catálogo de Publicações, pela mesma razão que a fase
                // o exige um nível acima: o código declarado aqui vira o nome do produto no
                // cronograma público e a chave pela qual a restauração da configuração congelada
                // reencontra o produto. Um código que o catálogo não conhece não resolve para
                // nada — e só apareceria depois de publicado.
                Result<ClassificacaoDoTipoDeAto?> congelada = await congelados
                    .DoTipoJaDeclaradoAsync(d.AtoCodigo, cancellationToken).ConfigureAwait(false);
                if (congelada.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(congelada.Error!);
                }

                ClassificacaoDoTipoDeAto classificacao;
                if (congelada.Value is { } daVersao)
                {
                    classificacao = daVersao;
                }
                else
                {
                    TipoAtoPublicadoView? tipoAto = await ResolverTipoDeAtoAsync(d.AtoCodigo).ConfigureAwait(false);
                    if (tipoAto is null)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
                            $"O tipo de ato '{d.AtoCodigo}', declarado pela etapa '{input.Nome}', não tem versão vigente no catálogo de Publicações na data de hoje."));
                    }

                    classificacao = ResolucaoDosTiposDeAtoUnicos.ClassificacaoDe(tipoAto);
                }

                // Papel preliminar ou definitivo só faz sentido sobre resultado: é o par que a
                // janela recursal ancora. A fase recusa o mesmo, e deixar passar aqui produziria
                // uma etapa cujo recurso aponta para um produto que nunca divulga resultado.
                if (papel is not null && !classificacao.EhResultado)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado",
                        $"O tipo de ato '{d.AtoCodigo}', declarado pela etapa '{input.Nome}', não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo."));
                }

                // O resultado sempre declara papel, e o tipo que congela a configuração não serve de
                // item de resultado (UNI-REQ-0157).
                if (classificacao.ViolacaoDoProduto(d.AtoCodigo, papel, $"na etapa '{input.Nome}'") is { } violacaoDoProduto)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(violacaoDoProduto);
                }

                produtosDaEtapa.Add(ProdutoDaEtapa.Criar(d.AtoCodigo, papel));
            }

            Result produtosResult = etapas[i].DefinirProdutos(produtosDaEtapa);
            if (produtosResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(produtosResult.Error!);
            }

            Result janelaResult = etapas[i].DefinirJanelaEParecer(
                input.Inicio, input.Fim, input.EmiteParecerIndividual);
            if (janelaResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(janelaResult.Error!);
            }

            List<BancaDaEtapa> bancas = [];
            foreach (BancaDaEtapaInput bancaInput in input.Bancas)
            {
                TipoBancaView? tipoBanca = await ResolverTipoDeBancaAsync(bancaInput.TipoBancaId)
                    .ConfigureAwait(false);
                if (tipoBanca is null)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "EtapaProcesso.TipoBancaNaoEncontradoOuInativo",
                        $"O tipo de banca {bancaInput.TipoBancaId} não existe ou está inativo no cadastro."));
                }

                bancas.Add(BancaDaEtapa.Criar(tipoBanca.Id, tipoBanca.Codigo));
            }

            Result bancasResult = etapas[i].DefinirBancas(bancas);
            if (bancasResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(bancasResult.Error!);
            }

            // A âncora em ato é declarada pelo código, e resolvida DENTRO dos produtos desta
            // etapa: ancorar na publicação de outra etapa deixa de ser exprimível.
            List<RecursoDaEtapa> recursos = [];
            foreach (RecursoDaEtapaInput declarado in input.Recursos)
            {
                // Entre os PRELIMINARES, e não entre todos os produtos: a etapa publica o mesmo
                // ato duas vezes — preliminar e definitivo —, e é do preliminar que a janela
                // corre. Resolver só pelo código devolveria o definitivo sempre que ele viesse
                // primeiro na coleção, e a etapa seria recusada por ancorar onde não se pode.
                ProdutoDaEtapa? produtoAncora = declarado.Ancora == AncoraDoRecurso.AtoPublicado
                    ? etapas[i].Produtos
                        .FirstOrDefault(pr => pr.Papel == PapelProdutoFase.Preliminar
                            && string.Equals(pr.AtoCodigo, declarado.AtoAncoraCodigo, StringComparison.Ordinal))
                    : null;
                Guid ancora = produtoAncora?.Id ?? Guid.Empty;

                // As mesmas duas recusas que a fase dá sobre a âncora dela, e pelas mesmas
                // razões. O ato que CONGELA a configuração é o que fixa as regras do certame:
                // contar prazo de recurso a partir dele é contar contra o documento que o
                // candidato não está contestando. E o ato de EFEITO IRREVERSÍVEL concede
                // direito que não se desfaz — uma vaga escassa já entregue a outro —, então o
                // recurso não teria o que reverter; o cabível é contra o resultado que o
                // fundamentou (UNI-REQ-0080).
                //
                // Conferido sobre o produto âncora JÁ RESOLVIDO, e não sobre o código cru do
                // payload: um código que não corresponde a produto preliminar algum desta etapa
                // não é uma âncora indevida, é uma âncora que não existe, e quem sabe dizer isso
                // — nomeando os preliminares disponíveis — é a etapa, logo abaixo. A memoização
                // por código evita reler o catálogo quando o produto acabou de ser resolvido;
                // quando ele já era declarado, esta é a única leitura que o confere.
                if (produtoAncora is not null)
                {
                    Result<ClassificacaoDoTipoDeAto?> ancoraCongelada = await congelados
                        .DoTipoJaDeclaradoAsync(produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
                    if (ancoraCongelada.IsFailure)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(ancoraCongelada.Error!);
                    }

                    TipoAtoPublicadoView? tipoAncora = await ResolverTipoDeAtoAsync(produtoAncora.AtoCodigo).ConfigureAwait(false);

                    // A irreversibilidade não é congelada: sem versão vigente no catálogo, ela é desconhecida,
                    // e uma âncora nova não pode ser aceita sem conferi-la (UNI-REQ-0080). A âncora que a
                    // versão publicada ou o agregado já tem segue valendo.
                    if (tipoAncora is null && !atosJaAncorados.Contains(produtoAncora.AtoCodigo))
                    {
                        Result<bool> ancoraJaPublicada = await congelados
                            .AncoraDaEtapaJaPublicadaAsync(etapas[i].Id, produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
                        if (ancoraJaPublicada.IsFailure)
                        {
                            unitOfWork.DescartarAlteracoesNaoSalvas();
                            return Result<MutacaoAceita>.Failure(ancoraJaPublicada.Error!);
                        }

                        if (!ancoraJaPublicada.Value)
                        {
                            unitOfWork.DescartarAlteracoesNaoSalvas();
                            return Result<MutacaoAceita>.Failure(new DomainError(
                                "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
                                $"O tipo de ato '{produtoAncora.AtoCodigo}', declarado pela etapa '{input.Nome}', não tem versão vigente no catálogo de Publicações na data de hoje, e a âncora nova precisa da irreversibilidade dele."));
                        }
                    }

                    bool ancoraCongela = ancoraCongelada.Value?.CongelaConfiguracao ?? tipoAncora?.CongelaConfiguracao ?? false;
                    if (ancoraCongela)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "RecursoDaEtapa.AncoraEmAtoCongelante",
                            $"O tipo de ato âncora '{produtoAncora.AtoCodigo}', declarado pela etapa '{input.Nome}', congela configuração — a âncora do recurso nunca é o ato que congela a configuração."));
                    }

                    if (tipoAncora?.EfeitoIrreversivel == true)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "RecursoDaEtapa.AncoraEmAtoIrreversivel",
                            $"O tipo de ato âncora '{tipoAncora.Codigo}', declarado pela etapa '{input.Nome}', tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta."));
                    }
                }

                ArgsRegraPrazoRecurso args = new(
                    declarado.PrazoValor,
                    declarado.PrazoUnidade,
                    declarado.SuspensividadePrimeiraInstanciaValor,
                    declarado.SuspensividadePrimeiraInstanciaUnidade,
                    declarado.AdmiteInstanciaSuperior,
                    declarado.SuspensividadeSegundaInstanciaValor,
                    declarado.SuspensividadeSegundaInstanciaUnidade);

                // A referência é montada a partir dos valores RESOLVIDOS do catálogo, nunca
                // ecoados do payload: é o que faz o hash bater por construção. E a recusa é
                // de validação, nunca exceção — catálogo que não respondeu na tela chega
                // aqui com código em branco, e o cliente precisa da mensagem, não de um 500.
                RegraCatalogo? regraCatalogo = await ResolverRegraAsync(declarado.RegraCodigo, declarado.RegraVersao)
                    .ConfigureAwait(false);
                if (regraCatalogo is null || regraCatalogo.Tipo != TipoRegra.RegraPrazoRecurso)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "RecursoDaEtapa.RegraCatalogoInvalida",
                        $"A regra {declarado.RegraCodigo}/{declarado.RegraVersao} não é uma regra de prazo de recurso do catálogo."));
                }

                Result<ReferenciaRegra> regraResult =
                    ReferenciaRegra.Criar(regraCatalogo.Codigo, regraCatalogo.Versao, regraCatalogo.Hash);
                if (regraResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(regraResult.Errors);
                }

                Result<RecursoDaEtapa> recursoResult = RecursoDaEtapa.Criar(
                    declarado.Ancora,
                    regraResult.Value!,
                    args,
                    ancora);
                if (recursoResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(recursoResult.Errors);
                }

                recursos.Add(recursoResult.Value!);
            }

            Result recursosResult = etapas[i].DefinirRecursos(recursos);
            if (recursosResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(recursosResult.Error!);
            }
        }

        Result result = processo.DefinirEtapas(etapas, command.Precondicao);
        if (result.IsFailure)
        {
            // Mesmo motivo do descarte acima: etapas existentes já foram mutadas
            // (AtualizarDados) no loop de reconciliação antes desta checagem — sem
            // descartar, o SaveChangesAsync automático do Wolverine persistiria a
            // mutação parcial mesmo com DefinirEtapas tendo recusado a coleção inteira.
            unitOfWork.DescartarAlteracoesNaoSalvas();

            // Propaga o lote: o fechamento do ciclo acumula uma violação por matéria aberta,
            // cada uma com o campo que a localiza; a forma serve também às recusas de erro
            // único do mesmo método.
            return Result<MutacaoAceita>.ValidationFailure(result.Errors);
        }

        // O agregado vem tracked de ObterParaMutacaoAsync: a substituição
        // da coleção (Clear + novos filhos com Guid v7 já preenchido) é
        // persistida por change detection no SaveChanges. NÃO chamar
        // DbSet.Update aqui — ele marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static IReadOnlyList<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        [.. erros.Select(erro => erro.Field is null ? erro : erro with { Field = $"etapas[{indice}].{erro.Field}" })];

    private static IReadOnlyList<FieldError> ErrosDeCarater(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, string nomeDoTipo, int indice) =>
        ComIndice(EtapaProcesso.ValidarCaraterAdmitido(carater, admitePontuacao, admiteEliminacao, nomeDoTipo), indice);
}
