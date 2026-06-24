namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// Registry de mapeamentos de erros de domínio do Configuracao para wire codes
/// / status HTTP. Cobre os cadastros <c>Campus</c> e <c>LocalOferta</c> e a
/// validação da referência de cidade do Geo (UNI-REQ #587 · ADR-0090).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>().")]
internal sealed class ConfiguracaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        // ── Referência de cidade do Geo (compartilhada por Campus e LocalOferta) ──
        new(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.codigo_ibge_obrigatorio",
                "Código IBGE da cidade é obrigatório")),

        new(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.codigo_ibge_formato_invalido",
                "Código IBGE da cidade em formato inválido")),

        new(CidadeReferenciaErrorCodes.UfObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.uf_obrigatoria",
                "UF da cidade é obrigatória")),

        new(CidadeReferenciaErrorCodes.UfIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.uf_incoerente",
                "UF informada incompatível com o prefixo do código IBGE")),

        new(CidadeReferenciaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.nome_obrigatorio",
                "Nome da cidade é obrigatório")),

        new(CidadeReferenciaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.nome_tamanho",
                "Nome da cidade excede o tamanho máximo")),

        // ── Referência de endereço estruturado ao Geo (ADR-0096) ──────────
        new(EnderecoReferenciaErrorCodes.CepObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.cep_obrigatorio",
                "CEP do endereço é obrigatório")),

        new(EnderecoReferenciaErrorCodes.CepFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.cep_formato_invalido",
                "CEP do endereço em formato inválido")),

        new(EnderecoReferenciaErrorCodes.LogradouroTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.logradouro_tamanho",
                "Logradouro do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.NumeroTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.numero_tamanho",
                "Número do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.ComplementoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.complemento_tamanho",
                "Complemento do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.BairroTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.bairro_tamanho",
                "Bairro do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.DistritoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.distrito_tamanho",
                "Distrito do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.nivel_resolucao_obrigatorio",
                "Nível de resolução do endereço é obrigatório")),

        new(EnderecoReferenciaErrorCodes.NivelResolucaoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.nivel_resolucao_invalido",
                "Nível de resolução do endereço inválido")),

        new(EnderecoReferenciaErrorCodes.OrigemObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.origem_obrigatoria",
                "Origem da resolução do endereço é obrigatória")),

        new(EnderecoReferenciaErrorCodes.OrigemTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.origem_tamanho",
                "Origem da resolução do endereço excede o tamanho máximo")),

        new(EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.latitude_fora_de_faixa",
                "Latitude do endereço fora da faixa válida")),

        new(EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.longitude_fora_de_faixa",
                "Longitude do endereço fora da faixa válida")),

        new(EnderecoReferenciaErrorCodes.CidadeIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.cidade_incoerente",
                "Cidade do endereço incoerente com a cidade informada")),

        new(EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.endereco_referencia.cidade_obrigatoria_com_endereco",
                "Cidade é obrigatória quando há endereço estruturado")),

        // ── Campus ────────────────────────────────────────────────────────
        new(CampusErrorCodes.SiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_obrigatoria",
                "Sigla do campus é obrigatória")),

        new(CampusErrorCodes.SiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_tamanho",
                "Tamanho da sigla do campus inválido")),

        new(CampusErrorCodes.SiglaJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.sigla_ja_existe",
                "Já existe um campus ativo com esta sigla")),

        new(CampusErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_obrigatorio",
                "Nome do campus é obrigatório")),

        new(CampusErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_tamanho",
                "Tamanho do nome do campus inválido")),

        new(CampusErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.codigo_emec_tamanho",
                "Tamanho do código e-MEC do campus inválido")),

        new(CampusErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.campus.nao_encontrado",
                "Campus não encontrado")),

        new(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.remocao_bloqueada_por_local_oferta",
                "Não é possível remover um campus responsável por locais de oferta ativos")),

        // ── LocalOferta ───────────────────────────────────────────────────
        new(LocalOfertaErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.tipo_invalido",
                "Tipo de local de oferta inválido")),

        new(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.campus_responsavel_nao_encontrado",
                "Campus responsável informado não encontrado")),

        new(LocalOfertaErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.codigo_emec_tamanho",
                "Tamanho do código e-MEC do local de oferta inválido")),

        new(LocalOfertaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.local_oferta.nao_encontrado",
                "Local de oferta não encontrado")),

        new(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.local_oferta.remocao_bloqueada_por_oferta_curso",
                "Não é possível remover um local de oferta referenciado por oferta de curso ativa")),

        // ── Referência de reserva demográfica (UNI-REQ-0065) ──────────────
        new(ReferenciaReservaDemograficaErrorCodes.CensoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.censo_obrigatorio",
                "Censo de referência é obrigatório")),

        new(ReferenciaReservaDemograficaErrorCodes.CensoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.censo_tamanho",
                "Tamanho do Censo de referência inválido")),

        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.referencia_reserva_demografica.censo_ja_existe",
                "Já existe uma referência ativa para este Censo")),

        new(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.percentual_fora_de_faixa",
                "Percentual fora do intervalo válido (0 a 100)")),

        new(ReferenciaReservaDemograficaErrorCodes.BaseLegalObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.base_legal_obrigatoria",
                "Base legal é obrigatória")),

        new(ReferenciaReservaDemograficaErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.referencia_reserva_demografica.base_legal_tamanho",
                "Tamanho da base legal inválido")),

        new(ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.referencia_reserva_demografica.nao_encontrada",
                "Referência de reserva demográfica não encontrada")),

        // ── Pesos do ENEM por grupo de área (UNI-REQ-0066) ────────────────
        new(PesoAreaEnemErrorCodes.ResolucaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.resolucao_obrigatoria",
                "Resolução é obrigatória")),

        new(PesoAreaEnemErrorCodes.ResolucaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.resolucao_tamanho",
                "Tamanho da resolução inválido")),

        new(PesoAreaEnemErrorCodes.GrupoCursoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.grupo_curso_invalido",
                "Grupo de curso fora do domínio de grupos de área do ENEM")),

        new(PesoAreaEnemErrorCodes.ParJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.peso_area_enem.par_ja_existe",
                "Já existe uma linha de pesos ativa para esta resolução e grupo de curso")),

        new(PesoAreaEnemErrorCodes.PesoNegativo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.peso_negativo",
                "Peso de área não pode ser negativo")),

        new(PesoAreaEnemErrorCodes.CorteRedacaoNegativo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.corte_redacao_negativo",
                "Corte de redação não pode ser negativo")),

        new(PesoAreaEnemErrorCodes.BaseLegalObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.base_legal_obrigatoria",
                "Base legal é obrigatória")),

        new(PesoAreaEnemErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.base_legal_tamanho",
                "Tamanho da base legal inválido")),

        new(PesoAreaEnemErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.peso_area_enem.nao_encontrado",
                "Linha de pesos do ENEM não encontrada")),
    ];
}
