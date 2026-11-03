namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDoGrafo
{
    internal static DomainError? Validar(GrafoConfiguracao grafo)
    {
        // Story #851 §3.5: lista de etapas vazia é estado válido (processo sem prova,
        // ex. SiSU) — a antiga recusa incondicional foi removida também aqui, espelhando
        // a mudança em DefinirEtapas.
        //
        // O Id vem congelado do envelope (D2) — e nem EtapaProcesso nem o agregado o
        // validavam: a unicidade era garantida só pelo handler de PUT /etapas. Um
        // envelope com dois ids iguais produziria duas etapas que o etapa_ref não
        // consegue distinguir, e o INSERT colidiria na chave primária.
        List<Guid> idsEtapas = [.. grafo.Etapas.Select(e => e.Id)];
        if (idsEtapas.Any(id => id == Guid.Empty))
        {
            return new DomainError(
                "ProcessoSeletivo.IdEtapaAusente",
                "Toda etapa restaurada deve declarar o Id congelado no envelope.");
        }

        if (idsEtapas.Distinct().Count() != idsEtapas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode aparecer mais de uma vez na configuração restaurada.");
        }

        List<int> ordensEtapas = [.. grafo.Etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        if (ordensEtapas.Distinct().Count() != ordensEtapas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo.");
        }

        // Mesma regra de DefinirEtapas e DefinirClassificacao, na mesma posição de
        // DefinirEtapas: sem o espelho, um envelope restaurado reintroduziria o estado que os
        // caminhos de gravação recusam.
        if (RegrasDasEtapas.ValidarEtapaDeNotaDoEnem(grafo.Etapas, grafo.Classificacao) is { } erroNotaDoEnem)
        {
            return erroNotaDoEnem;
        }

        if (grafo.Etapas.Count > 0 && !grafo.Etapas.Any(e => e.ComponeNota))
        {
            return new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.");
        }

        if (grafo.DistribuicaoVagas.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada.");
        }

        List<Guid> ofertas = [.. grafo.DistribuicaoVagas.Select(d => d.OfertaCursoOrigemId)];
        if (ofertas.Distinct().Count() != ofertas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo.");
        }

        // Mesma invariante de DefinirDistribuicaoVagas e DefinirOpcoesDeCurso: a restauração repõe
        // as duas dimensões sem passar por elas.
        // Versão publicada sempre declarou o número, na mesma faixa que DefinirOpcoesDeCurso aceita:
        // ausência ou valor fora de 1 e 2 é envelope que o domínio não produziria.
        if (grafo.NumeroDeOpcoesDeCurso is not (>= 1 and <= ProcessoSeletivo.MaximoDeOpcoesDeCurso))
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido,
                "O número de opções de curso da configuração restaurada deve ser 1 ou 2.");
        }

        if (RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(grafo.NumeroDeOpcoesDeCurso, grafo.DistribuicaoVagas.Count))
        {
            return RegrasDaDistribuicaoDeVagas.ErroDeOpcoesDeCursoAcimaDasOfertas();
        }

        // issue #848/ADR-0115 §3.7 — mesma checagem de DefinirDistribuicaoVagas: a
        // restauração aplica o grafo diretamente via AplicarGrafo, sem passar por
        // aquele método, então precisa repetir a validação de consistência.
        if (RegrasDaDistribuicaoDeVagas.HaAcaoQuandoIndeferidoDivergenteEntreOfertas(grafo.DistribuicaoVagas))
        {
            return new DomainError(
                "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
                "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.");
        }

        if (RegrasDaDistribuicaoDeVagas.RegraDeAplicabilidadeDivergenteEntreOfertas(grafo.DistribuicaoVagas) is { } regraDivergente)
        {
            return regraDivergente;
        }

        if (RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(grafo.DistribuicaoVagas) is { } regraIncoerente)
        {
            return regraIncoerente;
        }

        if (RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(grafo.DistribuicaoVagas) is { } remanejamentoIncoerente)
        {
            return remanejamentoIncoerente;
        }

        // O mesmo teto da gravação: sem o espelho, a restauração aplicaria uma lista que o PUT
        // seguinte da mesma lista recusaria.
        if (RegrasDoDesempate.ValidarQuantidadeDeCriteriosDesempate(grafo.CriteriosDesempate.Count) is [var excesso, ..])
        {
            return excesso.Error;
        }

        if (grafo.FatosColetados.Select(static f => f.Finalidade).Concat(grafo.GruposColetados.Select(static g => g.Finalidade)).Distinct().Order()
                .Select(finalidade => FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(
                    grafo.FatosColetados.Count(f => f.Finalidade == finalidade), grafo.GruposColetados.Where(g => g.Finalidade == finalidade))))
                .FirstOrDefault(static e => e.Count > 0) is [var itensEmExcesso, ..])
        {
            return itensEmExcesso.Error;
        }

        List<int> ordensDesempate = [.. grafo.CriteriosDesempate.Select(c => c.Ordem)];
        if (ordensDesempate.Distinct().Count() != ordensDesempate.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemDesempateDuplicada",
                "Cada critério de desempate deve ter uma ordem única dentro do processo.");
        }

        // INV-B6 — contra as etapas DO GRAFO. Se o codec regenerasse o etapa.Id em vez
        // de preservá-lo, é aqui que a restauração pararia: o etapaRef congelado deixa
        // de resolver.
        IEnumerable<CriterioDesempate> porEtapa = grafo.CriteriosDesempate
            .Where(static c => c.Args is ArgsDesempateMaiorNotaEtapa);
        foreach (CriterioDesempate criterio in porEtapa)
        {
            ArgsDesempateMaiorNotaEtapa args = (ArgsDesempateMaiorNotaEtapa)criterio.Args;
            if (!idsEtapas.Contains(args.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaRefDesempateInexistente",
                    $"O critério de desempate na ordem {criterio.Ordem} referencia a etapa {args.EtapaRef}, que não existe na configuração restaurada (INV-B6).");
            }
        }

        // Mesma regra de DefinirCriteriosDesempate e DefinirClassificacao, contra a
        // classificação DO GRAFO: sem o espelho, a restauração reintroduziria um desempate
        // por área que o quadro restaurado não tem.
        if (RegrasDoDesempate.PrimeiraViolacaoDoDesempatePorArea(RegrasDoDesempate.EmOrdem(grafo.CriteriosDesempate), grafo.Classificacao) is { } violacaoDoDesempate)
        {
            return violacaoDoDesempate;
        }

        // A checagem ENEM×eliminação e a da falta em dia de prova do ENEM declarada no
        // máximo uma vez NÃO se repetem aqui: grafo.Classificacao chega já construído por
        // ConfiguracaoClassificacao.Criar (via EnvelopeCodec.LerClassificacao) — se o
        // envelope violasse uma delas, a decodificação já teria falhado antes de
        // ValidarGrafo ser chamado.
        foreach (RegraEliminacao regra in grafo.Classificacao.RegrasEliminacao)
        {
            // INV-B4 — mesma proteção do INV-B6, para a eliminação que cita uma etapa.
            if (regra.Args.EtapaReferenciada() is { } etapaRef && !idsEtapas.Contains(etapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {etapaRef}, que não existe na configuração restaurada (INV-B4).");
            }

            if (RegrasDasEtapas.AusenciaNaEtapaDoEnem(regra.Args, grafo.Etapas))
            {
                return RegrasDasEtapas.AusenciaNaEtapaDoEnemRecusada();
            }
        }

        // Story #851 — cronograma restaurado: checagens estruturais equivalentes às de
        // DefinirCronogramaFases, sobre o GRAFO (não sobre o agregado corrente). O grafo
        // de precedências e a resolução de regra/ato âncora NÃO são reconferidos aqui —
        // são I/O, e o congelamento dos parâmetros na publicação (UNI-REQ-0019) proíbe reinterpretar um passado legitimamente publicado contra
        // o catálogo de hoje (mesma doutrina de LeitorEnvelope.Regra).
        if (grafo.CronogramaFases.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.CronogramaFasesVazio",
                "O processo deve ter ao menos uma fase no cronograma.");
        }

        List<int> ordensFases = [.. grafo.CronogramaFases.Select(f => f.Ordem)];
        if (ordensFases.Distinct().Count() != ordensFases.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemFaseDuplicada",
                "Cada fase deve ter uma ordem única dentro do cronograma.");
        }

        List<Guid> origensFases = [.. grafo.CronogramaFases.Select(f => f.FaseCanonicaOrigemId)];
        if (origensFases.Distinct().Count() != origensFases.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.FaseCanonicaDuplicada",
                "A mesma fase canônica não pode aparecer duas vezes no cronograma.");
        }

        // A bicondicional fase×etapa é lida pelo VÍNCULO, como na publicação: a etapa que
        // declara a fase em que acontece responde por si, e qualquer fase pode subdividir-se —
        // não só a que o cadastro marca como agrupadora. Aplicar aqui a regra global antiga
        // recusava a restauração de um envelope que a publicação aceita: o certame com todas as
        // etapas vinculadas a fases não agrupadoras podia ser publicado e decodificado, mas o
        // descarte da retificação ficava impossível.
        bool haFaseAgrupadoraSemEtapa = grafo.CronogramaFases.Any(f => f.AgrupaEtapas
            && !grafo.Etapas.Any(e => string.Equals(e.FaseCodigo, f.Codigo, StringComparison.Ordinal))
            && !grafo.Etapas.Any(static e => e.FaseCodigo is null));
        if (haFaseAgrupadoraSemEtapa)
        {
            return new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Há uma fase que agrupa etapas no cronograma restaurado, mas nenhuma etapa acontece nela.");
        }

        bool haEtapaSemFaseAlguma = grafo.Etapas.Any(static e => e.FaseCodigo is null)
            && !grafo.CronogramaFases.Any(static f => f.AgrupaEtapas);
        if (haEtapaSemFaseAlguma)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "Há etapa pontuada no grafo restaurado sem fase declarada, e nenhuma fase agrupa etapas.");
        }

        return ValidacaoDaArvoreRestaurada.Validar(grafo);
    }
}
