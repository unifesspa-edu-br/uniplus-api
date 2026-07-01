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

        // ── Tipo de documento (UNI-REQ-0013) ──────────────────────────────
        new(TipoDocumentoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.codigo_obrigatorio",
                "Código do tipo de documento é obrigatório")),

        new(TipoDocumentoErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.codigo_tamanho",
                "Tamanho do código do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_documento.codigo_ja_existe",
                "Já existe um tipo de documento ativo com este código")),

        new(TipoDocumentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.nome_obrigatorio",
                "Nome do tipo de documento é obrigatório")),

        new(TipoDocumentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.nome_tamanho",
                "Tamanho do nome do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.descricao_tamanho",
                "Tamanho da descrição do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.CategoriaInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.categoria_invalida",
                "Categoria do tipo de documento fora do domínio fechado")),

        new(TipoDocumentoErrorCodes.FormatosAceitosTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.formatos_aceitos_tamanho",
                "Tamanho dos formatos aceitos do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.TamanhoMaximoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tamanho_maximo_invalido",
                "Tamanho máximo em MB do tipo de documento deve ser positivo")),

        new(TipoDocumentoErrorCodes.TipoEquivalenteTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tipo_equivalente_tamanho",
                "Tamanho do tipo equivalente inválido")),

        new(TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tipo_equivalente_igual_codigo",
                "Um tipo de documento não pode ser equivalente a si mesmo")),

        new(TipoDocumentoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_documento.nao_encontrado",
                "Tipo de documento não encontrado")),

        // ── Condição de atendimento especializado (UNI-REQ-0012) ──────────
        new(CondicaoAtendimentoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_obrigatorio",
                "Código da condição de atendimento é obrigatório")),

        new(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_formato_invalido",
                "Código da condição de atendimento em formato inválido")),

        new(CondicaoAtendimentoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.condicao_atendimento.codigo_ja_existe",
                "Já existe uma condição de atendimento ativa com este código")),

        new(CondicaoAtendimentoErrorCodes.CodigoProtegidoNaoEditavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_protegido_nao_editavel",
                "O código da condição reservada não pode ser alterado")),

        new(CondicaoAtendimentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.nome_obrigatorio",
                "Nome da condição de atendimento é obrigatório")),

        new(CondicaoAtendimentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.nome_tamanho",
                "Tamanho do nome da condição de atendimento inválido")),

        new(CondicaoAtendimentoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.descricao_tamanho",
                "Tamanho da descrição da condição de atendimento inválido")),

        new(CondicaoAtendimentoErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.condicao_atendimento.nao_encontrada",
                "Condição de atendimento não encontrada")),

        new(CondicaoAtendimentoErrorCodes.RemocaoBloqueadaCodigoProtegido,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.condicao_atendimento.remocao_bloqueada_codigo_protegido",
                "A condição reservada não pode ser removida")),

        // ── Recurso de acessibilidade (UNI-REQ-0012) ──────────────────────
        new(RecursoAcessibilidadeErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.nome_obrigatorio",
                "Nome do recurso de acessibilidade é obrigatório")),

        new(RecursoAcessibilidadeErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.nome_tamanho",
                "Tamanho do nome do recurso de acessibilidade inválido")),

        new(RecursoAcessibilidadeErrorCodes.NomeJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.recurso_acessibilidade.nome_ja_existe",
                "Já existe um recurso de acessibilidade ativo com este nome")),

        new(RecursoAcessibilidadeErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.descricao_tamanho",
                "Tamanho da descrição do recurso de acessibilidade inválido")),

        new(RecursoAcessibilidadeErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.recurso_acessibilidade.nao_encontrado",
                "Recurso de acessibilidade não encontrado")),

        // ── Tipo de deficiência (UNI-REQ-0012) ────────────────────────────
        new(TipoDeficienciaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.nome_obrigatorio",
                "Nome do tipo de deficiência é obrigatório")),

        new(TipoDeficienciaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.nome_tamanho",
                "Tamanho do nome do tipo de deficiência inválido")),

        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_deficiencia.nome_ja_existe",
                "Já existe um tipo de deficiência ativo com este nome")),

        new(TipoDeficienciaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.descricao_tamanho",
                "Tamanho da descrição do tipo de deficiência inválido")),

        new(TipoDeficienciaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_deficiencia.nao_encontrado",
                "Tipo de deficiência não encontrado")),

        // ── Modalidade de concorrência (UNI-REQ-0011) ─────────────────────
        new(ModalidadeErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.codigo_obrigatorio",
                "Código da modalidade é obrigatório")),

        new(ModalidadeErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.codigo_formato_invalido",
                "Código da modalidade em formato inválido")),

        new(ModalidadeErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.codigo_ja_existe",
                "Já existe uma modalidade ativa com este código")),

        new(ModalidadeErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.descricao_tamanho",
                "Tamanho da descrição da modalidade inválido")),

        new(ModalidadeErrorCodes.NaturezaInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.natureza_invalida",
                "Natureza legal da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.ComposicaoVagasInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.composicao_vagas_invalida",
                "Composição de vagas da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.RegraRemanejamentoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.regra_remanejamento_invalida",
                "Regra de remanejamento da modalidade fora do domínio fechado")),

        new(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.natureza_remanejamento_incoerente",
                "Regra de remanejamento incoerente com a natureza legal da modalidade")),

        new(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.origem_obrigatoria_para_retira_de",
                "A modalidade com composição 'retira de' exige a modalidade de origem")),

        new(ModalidadeErrorCodes.OrigemApenasParaRetiraDe,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.origem_apenas_para_retira_de",
                "Somente a composição 'retira de' admite modalidade de origem")),

        new(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.argumento_remanejamento_obrigatorio",
                "Argumentos de remanejamento incompatíveis com a regra declarada")),

        new(ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.acao_indeferimento_invalida",
                "Ação no indeferimento fora do domínio fechado")),

        new(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.referencia_inexistente_ou_inativa",
                "Modalidade referenciada (origem/destino/par/fallback) inexistente ou inativa")),

        new(ModalidadeErrorCodes.RemocaoBloqueadaPorReferencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.modalidade.remocao_bloqueada_por_referencia",
                "Não é possível remover uma modalidade referenciada por outra modalidade ativa")),

        new(ModalidadeErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.modalidade.base_legal_tamanho",
                "Tamanho da base legal da modalidade inválido")),

        new(ModalidadeErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.modalidade.nao_encontrada",
                "Modalidade não encontrada")),

        // ── Fase canônica (UNI-REQ-0064) ──────────────────────────────────
        new(FaseCanonicaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_obrigatorio",
                "Código da fase canônica é obrigatório")),

        new(FaseCanonicaErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_formato_invalido",
                "Código da fase canônica em formato inválido")),

        new(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.codigo_fora_do_conjunto_canonico",
                "Código da fase fora do conjunto canônico das quatorze fases")),

        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.fase_canonica.codigo_ja_existe",
                "Já existe uma fase canônica ativa com este código")),

        new(FaseCanonicaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.nome_obrigatorio",
                "Nome da fase canônica é obrigatório")),

        new(FaseCanonicaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.nome_tamanho",
                "Tamanho do nome da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.descricao_tamanho",
                "Tamanho da descrição da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.DonoTipicoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.dono_tipico_obrigatorio",
                "Dono típico da fase canônica é obrigatório")),

        new(FaseCanonicaErrorCodes.DonoTipicoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.dono_tipico_invalido",
                "Dono típico da fase canônica fora do domínio fechado")),

        new(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.agrupa_etapas_apenas_avaliacao",
                "Apenas a fase de avaliação agrupa etapas pontuadas")),

        new(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.complementacao_apenas_fases_permitidas",
                "Complementação documental só é permitida nas fases de homologação e recursos")),

        new(FaseCanonicaErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fase_canonica.base_legal_tamanho",
                "Tamanho da base legal da fase canônica inválido")),

        new(FaseCanonicaErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.fase_canonica.nao_encontrada",
                "Fase canônica não encontrada")),

        // ── Tipo de banca (UNI-REQ-0064) ──────────────────────────────────
        new(TipoBancaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.codigo_obrigatorio",
                "Código do tipo de banca é obrigatório")),

        new(TipoBancaErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.codigo_formato_invalido",
                "Código do tipo de banca em formato inválido")),

        new(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.codigo_fora_do_conjunto_canonico",
                "Código do tipo de banca fora do conjunto canônico das quatro bancas")),

        new(TipoBancaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_banca.codigo_ja_existe",
                "Já existe um tipo de banca ativo com este código")),

        new(TipoBancaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.nome_obrigatorio",
                "Nome do tipo de banca é obrigatório")),

        new(TipoBancaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.nome_tamanho",
                "Tamanho do nome do tipo de banca inválido")),

        new(TipoBancaErrorCodes.FaseTipicaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.fase_tipica_tamanho",
                "Tamanho da fase típica do tipo de banca inválido")),

        new(TipoBancaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.descricao_tamanho",
                "Tamanho da descrição do tipo de banca inválido")),

        new(TipoBancaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_banca.nao_encontrado",
                "Tipo de banca não encontrado")),
    ];
}
